using AviaoCriarEmbarques.Helpers;
using AviaoCriarEmbarques.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.IdentityModel.Metadata;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace AviaoCriarEmbarques
{
    public class ValidacaoesAoCriarEmbarque : IPlugin
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

                HaAssentosDisponíveis(entityContext);

                AssentoOcupado(entityContext);
            }
            
            
            if (context.MessageName.ToLower().Trim() == "update")
            {
                //valida só para o assento nao deixando atualizar caso o assento novo não esteja disnonível


                if (!context.InputParameters.Contains("Target") && !(context.InputParameters["Target"] is Entity)) return;
                Entity entityContext = context.InputParameters["Target"] as Entity;

                #region verifica se veio o novo assento no contexto, se sim, salva a informação na variavel assento novo
                OptionSetValue novoAssento = null;
                if (entityContext.Attributes.Contains("academia_assento") && entityContext.Attributes["academia_assento"] is OptionSetValue)
                {
                    novoAssento = (OptionSetValue)entityContext.Attributes["academia_assento"];
                }
                #endregion 

                #region salva o guid do embarque na variavel embarqueGuid
                Guid embarqueGuid = new Guid();
                if (entityContext.Attributes.Contains("academia_embarqueid") && entityContext.Attributes["academia_embarqueid"] is Guid)
                {
                    embarqueGuid = (Guid)entityContext.Attributes["academia_embarqueid"];
                }
                #endregion

                #region vai ao banco pegar o aviao e o assento salvo antes dessa atualização ser consumada
                QueryExpression query = new QueryExpression("academia_embarque");
                query.ColumnSet = new ColumnSet("academia_aviao", "academia_assento"); 
                query.Criteria.AddCondition("academia_embarqueid", ConditionOperator.Equal, embarqueGuid);
                
                EntityCollection result = serviceGlobal.RetrieveMultiple(query);

                //salva as informações do aviao na variavel aviaoReference
                EntityReference aviaoReference = null;

                if (result.Entities[0].Attributes.Contains("academia_aviao") && result.Entities[0].Attributes["academia_aviao"] is EntityReference)
                {
                    aviaoReference = (EntityReference)result.Entities[0].Attributes["academia_aviao"];
                }

                //salva as informações do assento anterior na variavel assentoOnDb
                OptionSetValue assentoOnDb = null;

                if (result.Entities[0].Attributes.Contains("academia_assento") && result.Entities[0].Attributes["academia_assento"] is OptionSetValue)
                {
                    assentoOnDb = (OptionSetValue)result.Entities[0].Attributes["academia_assento"];
                }
                #endregion

                #region verifica se o assento novo nao está ocupado
                QueryExpression queryEmbarque = new QueryExpression("academia_embarque");
                queryEmbarque.ColumnSet = new ColumnSet("academia_embarqueid");
                queryEmbarque.Criteria.AddCondition("academia_aviao", ConditionOperator.Equal, aviaoReference.Id);
                queryEmbarque.Criteria.AddCondition("academia_assento", ConditionOperator.Equal, novoAssento.Value);

                if (serviceGlobal.RetrieveMultiple(queryEmbarque).Entities.Count != 0)
                {
                    throw new InvalidPluginExecutionException("O assento não está disponível!");
                }
                #endregion

                #region se chegou aqui vai atualizar o assento, o codigo abaixo é para atualizar o valor do assento no nome do embarque

                Entity embarqueAtualizado = new Entity("academia_embarque");
                embarqueAtualizado.Id = embarqueGuid;
                embarqueAtualizado["academia_name"] = $"{aviaoReference.Name} - Assento: {novoAssento.Value}";

                serviceGlobal.Update(embarqueAtualizado);

                #endregion


            }


        }

        //verifica se o assento já está ocupado
        private bool AssentoOcupado(Entity entityContext)
        {
            bool ocupado = false;

            EntityReference aviaoReference = null;
            OptionSetValue assento = null;
            

            if (entityContext.Attributes.Contains("academia_aviao") && entityContext.Attributes["academia_aviao"] is EntityReference)
            {
                aviaoReference = (EntityReference)entityContext.Attributes["academia_aviao"];

                if (aviaoReference == null)
                    throw new InvalidPluginExecutionException("Selecione um avião!");
            }

            if (entityContext.Attributes.Contains("academia_assento") && entityContext.Attributes["academia_assento"] is OptionSetValue)
            {
                assento = (OptionSetValue)entityContext.Attributes["academia_assento"];

                if(assento == null)
                    throw new InvalidPluginExecutionException("Selecione um assento!");
            }

            QueryExpression queryEmbarque = new QueryExpression("academia_embarque");
            queryEmbarque.ColumnSet = new ColumnSet("academia_embarqueid"); 
            queryEmbarque.Criteria.AddCondition("academia_aviao", ConditionOperator.Equal, aviaoReference.Id);
            queryEmbarque.Criteria.AddCondition("academia_assento", ConditionOperator.Equal, assento.Value);

            if (serviceGlobal.RetrieveMultiple(queryEmbarque).Entities.Count != 0)
            {
                throw new InvalidPluginExecutionException("O assento não está disponível!");
            }

            return ocupado;
        }

        //verifica se há assentos disponiveis no avião
        private bool HaAssentosDisponíveis(Entity entityContext)
        {
            bool disponivel = true;

            EntityReference aviaoReference = null;

            if (entityContext.Attributes.Contains("academia_aviao") && entityContext.Attributes["academia_aviao"] is EntityReference)
            {
                aviaoReference = (EntityReference)entityContext.Attributes["academia_aviao"];

                if (aviaoReference == null)
                    throw new InvalidPluginExecutionException("Selecione um avião!");
            }

            QueryExpression queryEmbarque = new QueryExpression("academia_embarque");
            queryEmbarque.ColumnSet = new ColumnSet("academia_embarqueid");
            queryEmbarque.Criteria.AddCondition("academia_aviao", ConditionOperator.Equal, aviaoReference.Id);

            if (serviceGlobal.RetrieveMultiple(queryEmbarque).Entities.Count < Listas.Assentos.Count == false)
            {
                throw new InvalidPluginExecutionException("Não há assentos disponíveis no avião!");
            }

            return disponivel;
        }
    }
}
