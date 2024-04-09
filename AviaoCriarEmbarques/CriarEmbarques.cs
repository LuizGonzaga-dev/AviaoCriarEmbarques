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


            if (context.MessageName.ToLower().Trim().Equals("create"))
            {
                if (!context.InputParameters.Contains("Target") && !(context.InputParameters["Target"] is Entity)) return;
                Entity entityContext = context.InputParameters["Target"] as Entity;

                this.HaAssentosLivres(entityContext);
            }
        }

        //verifica se há assentos livres
        private bool HaAssentosLivres(Entity entityContext)
        {
            bool resposta = false;
            #region verifica se o avião possue embarques
            string guidAviao = entityContext.Id.ToString();

            QueryExpression queryEmbarque = new QueryExpression("academia_embarque");
            queryEmbarque.ColumnSet = new ColumnSet("academia_assento", "academia_aviao", "academia_opcao");
            queryEmbarque.Criteria.AddCondition("academia_aviao", ConditionOperator.Equal, guidAviao);

            // Executar a consulta para obter os embarques associados ao avião
            EntityCollection result = serviceGlobal.RetrieveMultiple(queryEmbarque);
            
            int qtdAssentos = Listas.Assentos.Count;
            
            if (result.Entities.Count == qtdAssentos)
            {
                return false;
            }
            else
            {
                //tem pelo menos uma poltrona ocupada
                if(result.Entities.Count != 0)
                {

                    #region Portao: valor padrao, mas verifica se é possivel pegar o mesmo dos embarques que ja estao no banco de dados
                    string portao = Listas.Portoes.First();

                    if (result.Entities.First().Attributes.Contains("academia_opcao") && result.Entities.First().Attributes["academia_opcao"] is OptionSetValue)
                    {
                        OptionSetValue optionSetValue = (OptionSetValue) result.Entities.First().Attributes["academia_opcao"];
                        portao = optionSetValue.Value.ToString();
                    }
                    #endregion


                    List<string> assentosOcupados = new List<string>();

                    foreach (Entity entity in result.Entities)
                    {
                        if(entity.Attributes.Contains("academia_assento") && entity.Attributes["academia_assento"] is OptionSetValue)
                        {
                            OptionSetValue optionSetValue = (OptionSetValue)entity.Attributes["academia_assento"];
                            assentosOcupados.Add(optionSetValue.Value.ToString());
                        }
                    }

                    List<string> assentosVazios = GetAssentosVazios(Listas.Assentos, assentosOcupados);

                    SetCriarEmbarque(assentosVazios, portao, entityContext.Id);

                } // todas poltronas desocupadas
                else
                {
                    SetCriarEmbarque(Listas.Assentos, Listas.Portoes.First(),entityContext.Id);
                }

                resposta = true;
            }

            
            #endregion

            return resposta;
        }


        private List<string> GetAssentosVazios(List<string> todosAssentos, List<string> ocupados) 
        {
            return todosAssentos.Except(ocupados).ToList();
            //List<string> assentosVazios = new List<string>();

            //foreach(var assento in todosAssentos)
            //{
            //    if(!ocupados.Contains(assento))
            //        assentosVazios.Add(assento);
            //}

            //return assentosVazios;
        }

        public void SetCriarEmbarque(List<string> assentosVazios, string portao, Guid guidAviao)
        {

            foreach(string assento in  assentosVazios)
            {
                CreateEmbarqueModel model = new CreateEmbarqueModel
                {
                    Assento = assento,
                    GuidAviao = guidAviao,
                    Portao = portao,
                    Nome = $"Embarque-{DateTime.UtcNow.AddHours(-3).ToString("G")}"
                };

                CriarEmbarque(model);
            }
        }
        private bool CriarEmbarque(CreateEmbarqueModel model)
        {
            bool adicionado = true;
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
                adicionado=false;
            }
            

            return adicionado;
        }
    }
}
