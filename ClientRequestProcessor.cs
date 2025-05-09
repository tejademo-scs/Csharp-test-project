// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ClientRequestProcessor.cs" company="GE Vernova">
//      GE VERNOVA CONFIDENTIAL
//      Unpublished work © 2024 GE Vernova. All rights reserved.
//      This computer code is proprietary and highly confidential to GE Vernova
//      and/or its affiliates. It may not be used, disclosed, modified, transferred,
//      or reproduced without prior written consent, and must be returned on demand. 
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using Newtonsoft.Json;
using Polly.Retry;
using Polly;
using SoftwareBillOfMaterial.Data.Constants;
using System.Text;
using SoftwareBillOfMaterial.Data.Services.Contracts;
using SoftwareBillOfMaterial.Service.Server.Contract;
using System.Management;
using SoftwareBillOfMaterial.Library.SBOM;
using System.Collections.Concurrent;
using SoftwareBillOfMaterial.Service.Server.Model;
using SoftwareBillOfMaterial.Library.SBOM.Model;

namespace SoftwareBillOfMaterial.Service.Server.Service
{
    public class ClientRequestProcessor(ILoggerService logger, INodeConfigService nodeConfigService, ICycloneDxSbom cycloneDxSbom, ICredentialService credentialService) : IClientRequestProcessor
    {     
        /// <summary>
        ///  It sends requests to all clients to generate SBOM report. 
        /// </summary>
        /// <returns></returns>
        public async Task SendRequestToClients(SBOMConfiguration sbomConfiguration)
        {
            try
            {
                //Fetch all clients details from node configuration.       
                List<NodeDetail> VmList = GetClientNodeDetails();
                if(VmList.Count == 0)
                {
                    return;
                }

                ParallelOptions parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = 5 };
                //var tasks = new List<Task>();
                await Parallel.ForEachAsync(VmList, async (vm, CancellationToken) =>
                {
                    ConcurrentQueue<Exception> exceptions = new ConcurrentQueue<Exception>();
                    try {
                        var managementScope = GetManagementScope(vm.Ip, vm.UserName, vm.Password);
                        //tasks.Add(GenerateSBOMAsynchWithRetry(managementScope, customerName, siteName));
                        await GenerateSBOMAsynchWithRetry(managementScope, sbomConfiguration);
                    }
                    catch (Exception ex) {                       
                        exceptions.Enqueue(ex);
                    }

                    if (exceptions.Count > 0) {
                        throw new AggregateException(exceptions);                                           
                    }                   
                });

                //var tasks = new List<Task>();
                //This is For-each approach without TPL
                //foreach (var vm in VmList)
                //{
                //    // tasks.Add(SendRequestAsync(clientUrl, requestBody));
                //    logger.Log(LogType.Information, "Service.Server", "Requesting SBOM files from client : " + vm.Ip);
                //    var managementScope = GetManagementScope(vm.Ip, vm.UserName, vm.Password);
                //    tasks.Add(cycloneDxSbom.GenerateSBOM(false, managementScope, customerName, siteName));
                //};

                //await Task.WhenAll(tasks);

                logger.Log(LogType.Information, "Service.Server", "All clients's SBOM Files created");
            }
            catch (Exception ex)
            {
                if (ex is AggregateException) {
                    foreach (var exception in (ex as AggregateException).InnerExceptions) { 
                       logger.Log(ex, LogType.Error, "Service.Server", "Error while requesting SBOM files from clients Remotely.");
                    }
                }
                else
                {
                    logger.Log(ex, LogType.Error, "Service.Server", "Error while requesting SBOM files from clients Remotely.");
                }
            }
        }

        //This method is used in client server approach to send request sbom fileles asynch
        private async Task GenerateSBOMAsynchWithRetry(ManagementScope managementScope,SBOMConfiguration sbomConfiguration)
        {
            //Retry if any exception occure while gerating SBOM File
            AsyncRetryPolicy asyncRetryPolicy = Policy.Handle<Exception>()
                .RetryAsync(retryCount: 1,                
                onRetry: (exception, count) => {
                    var retrylog = $"Retrying time: {count} for {managementScope.Path.Server}";
                    Console.WriteLine(retrylog);
                    //Log method can't used here becoz it throws DBContext error in mutithread approach
                    // logger.Log(LogType.Information, "Service.Server", $"Retrying time: {count}");  
                });

            //Polly package is used to retry asynch
            await asyncRetryPolicy.ExecuteAsync( async () =>
                {
                    await cycloneDxSbom.GenerateSBOM(false, managementScope, sbomConfiguration);
                });            
        }

        private ManagementScope GetManagementScope(string ipAddress, string userName, string password)
        {
            ConnectionOptions connectionOptions = new ConnectionOptions
            {
                Username = userName,
                Password = password,
                //    Authority = $"ntlmdomain:HMI", 
                //Authority = "Kerberos:" + "HMI.local",
                Impersonation = ImpersonationLevel.Impersonate,
                Authentication = AuthenticationLevel.PacketPrivacy,
                EnablePrivileges = true
            };

            string path = string.Format(@"\\{0}\root\cimv2", ipAddress);
            ManagementScope managementScope = new ManagementScope(path, connectionOptions);
            return managementScope;
        }
        
        private List<NodeDetail> GetClientNodeDetails()
        {
            //Fetch all client IP address from database
            var nodeConfigurations = nodeConfigService.GetAllNodeConfigurations().Where(x => !x.IsServer).ToList();
            var nodeDetails = new List<NodeDetail>();

            foreach (var nodeConfiguration in nodeConfigurations)
            {
                var credentialDetail = credentialService.GetCredential(nodeConfiguration.NodeId).FirstOrDefault();
                var nodeDetail= new NodeDetail() { Ip = nodeConfiguration.IpAddress1 , UserName = credentialDetail?.Username, Password = credentialDetail?.Password };
                nodeDetails.Add(nodeDetail);
            }

            return nodeDetails;
        }

        #region Methods used in client server architecture
        public List<string> GetClientApiUrls()
        {
            //Fetch all client IP address from database
            var clientIpAddresses = nodeConfigService.GetAllNodeConfigurations().Where(x => !x.IsServer).Select(x => x.IpAddress1).ToList();
            var clientApiUrls = new List<string>();

            foreach (var clientIp in clientIpAddresses)
            {
                var clientApiUrl = $"http://{clientIp}:7179/sbomthickclientapi";   // example : "http://192.168.20.13:7179/sbomthickclientapi"
                clientApiUrls.Add(clientApiUrl);
            }

            return clientApiUrls;
        }

        /// <summary>
        /// It sends requests to all clients to generate SBOM report.
        /// </summary>
        /// <param name="clientUrls"></param>
        /// <param name="requestBody"></param>
        /// <returns></returns>
        public async Task SendRequestsAsync(IEnumerable<string> clientUrls, string requestBody)
        {
            try
            {
                var tasks = new List<Task>();
                foreach (var clientUrl in clientUrls)
                {
                    tasks.Add(SendRequestAsync(clientUrl, requestBody));
                }
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                logger.Log(ex, LogType.Error, "Service.Server", "Error while requesting SBOM files from clients.");
            }
        }

        //This method is used in client server approach to send request sbom fileles asynch
        private async Task SendRequestAsync(string url, string jsonMessage)
        {
            try
            {
                HttpClient? _httpClient = new HttpClient();
                var stringContent = new StringContent(JsonConvert.SerializeObject(jsonMessage), Encoding.UTF8, "application/json");

                //Retry if HttpRequestException
                AsyncRetryPolicy<HttpResponseMessage> asyncRetryPolicy = Policy<HttpResponseMessage>.Handle<HttpRequestException>()
                    .WaitAndRetryAsync(retryCount: 3,
                    count => TimeSpan.FromSeconds(10),
                    onRetry: (exception, count, context) => {
                        logger.Log(LogType.Information, "Service.Server", $"Retrying time: {count} for URL: {context["URL"]}");

                    });

                var contextDictionary = new Dictionary<string, object>() { { "URL", url } };
                //Polly package is used to retry asynch
                var httpResponse = await asyncRetryPolicy.ExecuteAsync(async (Context) =>
                {
                    var response = await _httpClient.PostAsync(url, stringContent);
                    return response;
                }, contextDictionary);

                if (httpResponse.IsSuccessStatusCode)
                {
                    var responseBody = await httpResponse.Content.ReadAsStringAsync();
                    logger.Log(LogType.Information, "Service.Server", $"Response: {responseBody}");
                }
                else
                {
                    logger.Log(LogType.Warning, "Service.Server", $"Failed to send message.Status Code: {httpResponse.StatusCode}");
                }
            }
            //catch (HttpRequestException ex)
            //{
            //    logger.Log(ex, LogType.Error, "Service.Server", $"HttpRequestException while requesting SBOM files from client: {url}.");
            //}
            catch (Exception ex)
            {
                logger.Log(ex, LogType.Error, "Service.Server", $"Error while requesting SBOM files from client: {url}");
            }
        }
        #endregion
    }
}
