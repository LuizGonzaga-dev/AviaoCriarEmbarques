using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using AviaoCriarEmbarques.Helpers;
using static AviaoCriarEmbarques.Helpers.Listas;
using AviaoCriarEmbarques.Models;
using Microsoft.Crm.Sdk.Messages;
using System.CodeDom;
using System.IdentityModel.Metadata;

namespace AviaoCriarEmbarques
{
    public class CriarEmbarques : IPlugin
    {
        private IPluginExecutionContext context { get; set; }
        private IOrganizationServiceFactory serviceFactory { get; set; }
        private IOrganizationService serviceUsuario { get; set; }
        private IOrganizationService serviceGlobal { get; set; }
        private ITracingService tracing { get; set; }



        public void Execute(IServiceProvider serviceProvider)
        {
            #region "Cabeçalho essenciais para o plugin"            
            //Contexto de execução
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            //Fabrica de conexões
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            //Service no contexto do usuário
            serviceUsuario = serviceFactory.CreateOrganizationService(context.UserId);
            //Service no contexto Global (usuário System)
            serviceGlobal = serviceFactory.CreateOrganizationService(null);
            //Trancing utilizado para reastreamento de mensagem durante o processo
            tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            #endregion

            #region "Verificador de profundidade para evitar loop"
            if (context.Depth > 1) return;
            #endregion

            if (context.MessageName.ToLower().Trim().Equals("create"))
            {
                if (!context.InputParameters.Contains("Target") && !(context.InputParameters["Target"] is Entity)) return;
                Entity entityContext = context.InputParameters["Target"] as Entity;

                GerarEmbarques(entityContext);
            }
        }

        private List<EmbarqueModel> GetEmbarques(Entity entityContext)
        {
            List<EmbarqueModel> embarques = new List<EmbarqueModel>();

            try
            {
                string guidAviao = entityContext.Id.ToString();

                QueryExpression queryEmbarque = new QueryExpression("academia_embarque");
                queryEmbarque.ColumnSet = new ColumnSet("academia_assento", "academia_aviao", "academia_opcao");
                queryEmbarque.Criteria.AddCondition("academia_aviao", ConditionOperator.Equal, guidAviao);

                EntityCollection result = serviceGlobal.RetrieveMultiple(queryEmbarque);

                int qtdAssentos = Listas.Assentos.Count;

                foreach (Entity entity in result.Entities)
                {
                    String aviaoName = null, assento = null, portao = null;
                    Guid guidEmbarque = new Guid();

                    if (entity.Attributes.Contains("academia_assento") && entity.Attributes["academia_assento"] is OptionSetValue)
                    {
                        OptionSetValue optionSetValue = (OptionSetValue)entity.Attributes["academia_assento"];
                        assento = optionSetValue.Value.ToString();
                    }

                    if (entity.Attributes.Contains("academia_opcao") && entity.Attributes["academia_opcao"] is OptionSetValue)
                    {
                        OptionSetValue optionSetValue = (OptionSetValue)entity.Attributes["academia_opcao"];
                        portao = optionSetValue.Value.ToString();
                    }

                    if (entity.Attributes.Contains("academia_aviao") && entity.Attributes["academia_aviao"] is EntityReference)
                    {
                        EntityReference reference = (EntityReference)entity.Attributes["academia_aviao"];
                        aviaoName = reference.Name;
                    }

                    if (entity.Attributes.Contains("academia_embarqueid") && entity.Attributes["academia_embarqueid"] is Guid)
                    {
                        guidEmbarque = (Guid)entity.Attributes["academia_embarqueid"];
                    }

                    EmbarqueModel embarque = new EmbarqueModel() { 
                        Assento = assento,
                        AviaoName = aviaoName,
                        Portao = portao,
                        AviaoGuid = entityContext.Id,
                        EmbarqueGuid = guidEmbarque
                    };

                    embarques.Add(embarque);
                }

            }
            catch(Exception ex)
            {
                Console.WriteLine( "Ocorreu um erro: " + ex.Message);
                return null;
            }

            return embarques;
        }

        private void GerarEmbarques(Entity entityContext)
        {
            //Verifica se há embarques no aviao
            List<EmbarqueModel> embarques = GetEmbarques(entityContext);

            if (embarques == null)
            {
                //Houve algum problema ao consultar os embarques no banco de dados
                throw new InvalidPluginExecutionException("Não foi possível consultar os embarques do avião.");               
            }
            else if(embarques.Count == Listas.Assentos.Count)
            {
                //todos os assentos estão ocupados
                throw new InvalidPluginExecutionException("Não há assentos disponíveis no avião");
            }
            else if (embarques.Count == 0)
            {
                //Todas as poltronas estao livres
                SetCriarEmbarque( entityContext );
            }
            else 
            {
                //pelo menos uma poltrona está ocupada, busca pelos assentos vazios, salva na lista e passa para o método SetCriarEmbarque criá-los
                List<CreateEmbarqueModel> assentosVazios = GetAssentosVazios(Listas.Assentos, embarques);
                SetCriarEmbarque(assentosVazios);

            }
        }

        private List<CreateEmbarqueModel> GetAssentosVazios(List<string> todosAssentos, List<EmbarqueModel> ocupados)
        {
            List<CreateEmbarqueModel> assentosVaziosModel = new List<CreateEmbarqueModel>();

            EmbarqueModel embarque = ocupados.First();

            var assentosVazios = todosAssentos.Except(ocupados.Select(x => x.Assento).ToList()).ToList();

            foreach (string assento in assentosVazios)
            {
                assentosVaziosModel.Add(new CreateEmbarqueModel
                {
                    Assento = assento,
                    GuidAviao = embarque.AviaoGuid,
                    Nome = $"{embarque.AviaoName} - Assento: {assento}",
                    Portao = embarque.Portao
                });
            }

            return assentosVaziosModel;
        }

        public void SetCriarEmbarque(Entity entityContext)
        {
            List<string> todosAssentos = Listas.Assentos;
            string aviaoName = "Avião";

            if (entityContext.Attributes.Contains("academia_name") && entityContext.Attributes["academia_name"] is String)
            {
                aviaoName = (string)entityContext.Attributes["academia_name"];
            }

            foreach (string assento in todosAssentos)
            {
                CreateEmbarqueModel model = new CreateEmbarqueModel
                {
                    Assento = assento,
                    GuidAviao = entityContext.Id,
                    Portao = Listas.Portoes.First(),
                    Nome = $"{aviaoName} - Assento: {assento}"
                };

                CriarEmbarque(model);
            }
        }

        public void SetCriarEmbarque(List<CreateEmbarqueModel> model)
        {

            foreach (CreateEmbarqueModel m in model)
            {
                CriarEmbarque(m);
            }
        }

        private void CriarEmbarque(CreateEmbarqueModel model)
        {
            try
            {
                Entity embarque = new Entity("academia_embarque");
                embarque["academia_name"] = model.Nome;
                embarque["academia_assento"] = new OptionSetValue(Convert.ToInt32(model.Assento));
                embarque["academia_opcao"] = new OptionSetValue(Convert.ToInt32(model.Portao));
                embarque["academia_aviao"] = new EntityReference("academia_aviao", model.GuidAviao);

                serviceGlobal.Create(embarque);
            }catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw new InvalidPluginExecutionException("Não foi possível criar os embarques.");
            }
            
        }
    }
}
