using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using Blocto.Sdk.Core.Utility;
using Blocto.Sdk.Flow.Model;
using Flow.FCL.Extensions;
using Flow.FCL.Models;
using Flow.FCL.Models.Authn;
using Flow.FCL.Models.Authz;
using Flow.FCL.Utility;
using Flow.FCL.WalletProvider;
using Flow.Net.Sdk.Core;
using Flow.Net.Sdk.Core.Client;
using Flow.Net.Sdk.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Blocto.SDK.Flow
{
    public class BloctoWalletProvider : IWalletProvider
    {
        
        /// <summary>
        /// System Thumbnail
        /// </summary>
        private static string Thumbnail;
        
        /// <summary>
        /// System Title
        /// </summary>
        private static string Title;
        
        public static Dictionary<string, string> AuthnRequestMapper;
        
        public static Dictionary<string, string> SignMessageRecordStore;

        public static Dictionary<string, TransactionProcessData> TransactionRecordStore;

        private ILogger<BloctoWalletProvider> _logger;

        private IResolveUtility _resolveUtility;
        
        private IFlowClient _flowClient;
        
        private IHttpClientFactory _httpClientFactory;
        
        private JsonNetSerializer _serializer;
        
        private Guid _bloctoAppIdentifier;
        
        private string _appSdkDomain = "https://staging.blocto.app/sdk?";
        
        private string _backedApiDomain = "https://api.blocto.app";
        
        public string _address = "default";
        
        private List<Func<string, (int Index, string Name, string Value)>> _authnReturnParsers;

        static BloctoWalletProvider()
        {
            BloctoWalletProvider.TransactionRecordStore = new Dictionary<string, TransactionProcessData>();
            BloctoWalletProvider.AuthnRequestMapper = new Dictionary<string, string>();
            BloctoWalletProvider.SignMessageRecordStore = new Dictionary<string, string>();
        }
        
        /// <summary>
        /// Create blocto wallet provider instance
        /// </summary>
        /// <returns>BloctoWalletProvider</returns>
        public BloctoWalletProvider(ILogger<BloctoWalletProvider> logger, IHttpClientFactory httpClientFactory, IFlowClient flowClient, IResolveUtility resolveUtility, string env, Guid appIdentifier)
        {
            _httpClientFactory = httpClientFactory;
            _flowClient = flowClient;
            _resolveUtility = resolveUtility;
            _bloctoAppIdentifier = appIdentifier;
            _logger = logger;
            _serializer = new JsonNetSerializer();
            
            if(env.ToLower() == "testnet")
            {
                _backedApiDomain = _backedApiDomain.Replace("api", "api-dev");
            } 
        }

        /// <summary>
        /// User connect wallet get account
        /// </summary>
        /// <param name="url">fcl authn url</param>
        /// <param name="parameters">parameter of authn</param>
        /// <param name="internalCallback">After, get endpoint response internal callback.</param>
        public async Task<(string Url, string AuthenticationId)> AuthenticateAsync(string url, Dictionary<string, object> parameters, Action<object> internalCallback = null!)
        {
            var payload = new StringContent(JsonConvert.SerializeObject(parameters), Encoding.UTF8, MediaTypeNames.Application.Json);            
            var httpRequestMessage = new HttpRequestMessage( HttpMethod.Post, url)
                                     {
                                         Headers =
                                         {
                                             { HeaderNames.Accept, "application/json" },
                                             { "Blocto-Application-Identifier", _bloctoAppIdentifier.ToString() }
                                         },
                                         Content = payload
                                     };

            var httpClient = _httpClientFactory.CreateClient();
            var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                await using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync();
                var authnResponse = _serializer.DeserializeFormStream<AuthnAdapterResponse>(contentStream);
                var endpoint = authnResponse.AuthnEndpoint();
                BloctoWalletProvider.AuthnRequestMapper.Add(authnResponse.Local.Params.AuthenticationId, endpoint.PollingUrl.AbsoluteUri);
                BloctoWalletProvider.Thumbnail = authnResponse.Local.Params.Thumbnail;
                BloctoWalletProvider.Title = authnResponse.Local.Params.Title;
                
                return (endpoint.IframeUrl, authnResponse.Local.Params.AuthenticationId);
            }
            
            throw new Exception("Get authn information failed.");
        }
        
        public async Task AuthenticateResultAsync(string authenticationId, Action<object> internalCallback = null!)
        {
            var url = BloctoWalletProvider.AuthnRequestMapper[authenticationId];
            var httpRequestMessage = new HttpRequestMessage( HttpMethod.Get, url)
                                     {
                                         Headers =
                                         {
                                             { HeaderNames.Accept, "application/json" },
                                             { "Blocto-Application-Identifier", _bloctoAppIdentifier.ToString() }
                                         }
                                     };

            var httpClient = _httpClientFactory.CreateClient();
            var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                await using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync();
                var response = _serializer.DeserializeFormStream<AuthenticateResponse>(contentStream);
                internalCallback.Invoke(response);
                return;
            }
            
            throw new Exception("Get authn information failed.");
        }

        
        /// <summary>
        /// Send transaction
        /// </summary>
        /// <param name="service">fcl service for web sdk</param>
        /// <param name="tx">flow transaction data</param>
        /// <param name="internalCallback">complete transaction internal callback</param>
        public virtual async Task<(string Url, string AuthorizationId, string SessionId)> SendTransaction(FclService service, FlowTransaction tx, Action<string> internalCallback)
        {
            try
            {
                var url = service.PreAuthzEndpoint();
                var preSignableJObj = _resolveUtility.ResolvePreSignable(ref tx);
                var payload = new StringContent(JsonConvert.SerializeObject(preSignableJObj), Encoding.UTF8, MediaTypeNames.Application.Json); 
                var httpRequestMessage = new HttpRequestMessage( HttpMethod.Post, url)
                                         {
                                             Headers =
                                             {
                                                 { HeaderNames.Accept, "application/json" },
                                                 { "Blocto-Application-Identifier", _bloctoAppIdentifier.ToString() }
                                             },
                                             Content = payload
                                         };

                var httpClient = _httpClientFactory.CreateClient();
                var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);

                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    await using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync();
                    var response = _serializer.DeserializeFormStream<PreAuthzAdapterResponse>(contentStream);
                    var tmpAccount = await GetAccountAsync(response.AuthorizerData.Proposer.Identity.Address);
                    tx.ProposalKey = GetProposerKey(tmpAccount, response.AuthorizerData.Proposer.Identity.KeyId);
                    var result = await CustodialHandler(tx, response, null);
                    return result;
                }
                
                throw new Exception("Create transaction failed.");
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message, e);
                throw;
            }
        }
        
        public async Task<string> SendTransactionResultAsync(string authorizationId, string sessionId)
        {
            var key = $"{authorizationId}-{sessionId}";
            var data = BloctoWalletProvider.TransactionRecordStore[key];
            var tx = data.Transaction;
            
            var httpRequestMessage = new HttpRequestMessage( HttpMethod.Get, data.PollingUrl)
                                     {
                                         Headers =
                                         {
                                             { HeaderNames.Accept, "application/json" },
                                             { "Blocto-Application-Identifier", _bloctoAppIdentifier.ToString() }
                                         }
                                     };

            var httpClient = _httpClientFactory.CreateClient();
            var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                await using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync();
                var response = _serializer.DeserializeFormStream<AuthzAdapterResponse>(contentStream); 
                var signInfo = response.SignatureInfo();
                if (signInfo.Signature != null)
                {
                    var payloadSignature = tx.PayloadSignatures.First(p => p.Address.Address == signInfo.Address?.ToString().RemoveHexPrefix());
                    payloadSignature.Signature = signInfo.Signature?.ToString().StringToBytes().ToArray();
                }
                
                var payerEndpoint = data.PreAuthzAdapterResponse.PayerEndpoint();
                var payerSignable = _resolveUtility.ResolvePayerSignable(ref tx, data.SignableObj);
                var payerSignResponse = await PayerHandler(payerEndpoint.AbsoluteUri, payerSignable);
                signInfo = payerSignResponse.SignatureInfo();
                if (signInfo.Signature != null && signInfo.Address != null)
                {
                    var envelopeSignature = tx.EnvelopeSignatures.First(p => p.Address.Address == signInfo.Address.ToString().RemoveHexPrefix());
                    envelopeSignature.Signature = signInfo.Signature?.ToString().StringToBytes().ToArray();
                }
                
                var txResponse = _flowClient.SendTransactionAsync(tx).ConfigureAwait(false).GetAwaiter().GetResult();
                return txResponse.Id;
            }
            
            throw new Exception("Get authz response failed.");
        }
        
        private async Task<SignatureResponse> PayerHandler(string url, JObject payerSignable)
        {
            var payload = new StringContent(JsonConvert.SerializeObject(payerSignable), Encoding.UTF8, MediaTypeNames.Application.Json); 
            var httpRequestMessage = new HttpRequestMessage( HttpMethod.Post, url)
                                     {
                                         Headers =
                                         {
                                             { HeaderNames.Accept, "application/json" },
                                             { "Blocto-Application-Identifier", _bloctoAppIdentifier.ToString() }
                                         },
                                         Content = payload
                                     };

            var httpClient = _httpClientFactory.CreateClient();
            var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                await using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync();
                var response = _serializer.DeserializeFormStream<SignatureResponse>(contentStream);
                return response;
            } 
            
            throw new Exception("Get payer signable failed.");
        }

        /// <summary>
        /// Handle custodial mode transaction
        /// </summary>
        /// <param name="tx">transaction data</param>
        /// <param name="preAuthzResponse">pre authz response data</param>
        /// <param name="callback"></param>
        /// <returns>Flow transaction data</returns>
        private async Task<(string Url, string AuthorizationId, string SessionId)> CustodialHandler(FlowTransaction tx, PreAuthzAdapterResponse preAuthzResponse, Action<string> callback)
        {
            var authorization = preAuthzResponse.AuthorizerData.Authorizations.First();
            var postUrl = authorization.AuthzAdapterEndpoint();
            var authorize = authorization.ConvertToFlowAccount();
            var signableJObj = _resolveUtility.ResolveSignable(ref tx, preAuthzResponse.AuthorizerData, authorize).First();
            var payload = new StringContent(JsonConvert.SerializeObject(signableJObj), Encoding.UTF8, MediaTypeNames.Application.Json); 
            var httpRequestMessage = new HttpRequestMessage( HttpMethod.Post, postUrl)
                                     {
                                         Headers =
                                         {
                                             { HeaderNames.Accept, "application/json" },
                                             { "Blocto-Application-Identifier", _bloctoAppIdentifier.ToString() }
                                         },
                                         Content = payload
                                     };

            var httpClient = _httpClientFactory.CreateClient();
            var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                await using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync();
                var response = _serializer.DeserializeFormStream<AuthzAdapterResponse>(contentStream);
                
                var authenticationId = response.AuthorizationUpdates.Params.GetValue("authorizationId");
                var sessionId = response.AuthorizationUpdates.Params.GetValue("sessionId");
                var key = $"{authenticationId}-{sessionId}";
                var endpoint = response.AuthzEndpoint();
                BloctoWalletProvider.TransactionRecordStore.Add(key, new TransactionProcessData
                                                                     {
                                                                         PreAuthzAdapterResponse = preAuthzResponse,
                                                                         Transaction = tx,
                                                                         PollingUrl = endpoint.PollingUrl.AbsoluteUri,
                                                                         SignableObj = signableJObj
                                                                     });
                
                return (endpoint.IframeUrl, authenticationId.ToString(), sessionId.ToString());
            } 
            
            throw new Exception("Get authz failed.");
        }
        
        /// <summary>
        /// SignMessage
        /// </summary>
        /// <param name="message">Original message </param>
        /// <param name="signService">FCL signature service</param>
        /// <param name="callback">After, get endpoint response callback.</param>
        public async Task<(string Url, string SignatureId)> SignMessageAsync(string message, FclService signService)
        {
            var signUrl = signService.SignMessageAdapterEndpoint();
            var hexMessage = message.StringToHex();
            var payloadObj = _resolveUtility.ResolveSignMessage(hexMessage, signService.PollingParams.SessionId());
            var payload = new StringContent(JsonConvert.SerializeObject(payloadObj), Encoding.UTF8, MediaTypeNames.Application.Json); 
            var httpRequestMessage = new HttpRequestMessage( HttpMethod.Post, signUrl)
                                     {
                                         Headers =
                                         {
                                             { HeaderNames.Accept, "application/json" },
                                             { "Blocto-Application-Identifier", _bloctoAppIdentifier.ToString() }
                                         },
                                         Content = payload
                                     };

            var httpClient = _httpClientFactory.CreateClient();
            var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                await using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync();
                var response = _serializer.DeserializeFormStream<AuthnAdapterResponse>(contentStream);
                var endpoint = response.SignMessageEndpoint();
                BloctoWalletProvider.SignMessageRecordStore.Add(response.Local.Params.SignatureId, endpoint.PollingUrl.AbsoluteUri);
                var webSb = new StringBuilder(endpoint.IframeUrl);
                webSb.Append("&")
                     .Append(Uri.EscapeDataString("thumbnail") + "=")
                     .Append(BloctoWalletProvider.Thumbnail + "&")
                     .Append(Uri.EscapeDataString("title") + "=")
                     .Append(Uri.EscapeDataString(BloctoWalletProvider.Title));
                return (webSb.ToString(), response.Local.Params.SignatureId);
            }

            throw new Exception("Sign message failed.");
        }
        
        public async Task<List<FlowSignature>> SignMessageResultAsync(string signatureId)
        {
            var url = BloctoWalletProvider.SignMessageRecordStore[signatureId];
            var httpRequestMessage = new HttpRequestMessage( HttpMethod.Get, url)
                                     {
                                         Headers =
                                         {
                                             { HeaderNames.Accept, "application/json" },
                                             { "Blocto-Application-Identifier", _bloctoAppIdentifier.ToString() }
                                         },
                                     };

            var httpClient = _httpClientFactory.CreateClient();
            var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                await using var contentStream = await httpResponseMessage.Content.ReadAsStreamAsync();
                var response = _serializer.DeserializeFormStream<SignMessageResponse>(contentStream);
                var signature = response?.Data.First().SignatureStr();
                var keyId = Convert.ToUInt32(response?.Data.First().KeyId());
                var addr = response?.Data.First().Address();
                var result = new List<FlowSignature>
                             {
                                 new FlowSignature
                                 {
                                     Address = new FlowAddress(addr),
                                     KeyId = keyId,
                                     Signature = Encoding.UTF8.GetBytes(signature!)
                                 }
                             };
                
                return result;
            }
            
            throw new Exception("Get signmessage failed.");
        }

        
        private (string Address, List<FlowSignature> Signatures) UniversalLinkAuthnHandler(string link)
        {
            var address = default(string);
            var signatures = default(List<FlowSignature>);
            var keywords = new List<string>{ "address=", "account_proof" };
            var index = 0;
            var data = (MatchContents: new List<string>(), RemainContent: link);
            while (data.RemainContent.Length > 0)
            {
                var keyword = keywords[index];
                data = CheckContent(data.RemainContent, keyword);
                switch (keyword)
                {
                    case "address=":
                        address = AddressParser(data.MatchContents.FirstOrDefault()).Value;
                        break;
                    case "account_proof":
                        signatures = SignatureProcess(data);
                        break;
                }
                
                index++;
            }
            
            return (address, signatures);
        }
        
        private List<FlowSignature> UniversalLinkSignMessageHandler(string link)
        {
            var data = CheckContent(link, "user_signature");
            var signatures = SignatureProcess(data);
            return signatures;
        }
        
        private string UniversalLinkTransactionHandler(string link)
        {
            var data = CheckContent(link, "tx_hash");
            var tx = data.MatchContent.First().Split("=")[1];
            return tx;
        }

        private List<FlowSignature> SignatureProcess((List<string> MatchContents, string RemainContent) data)
        {
            var sort = 0;
            var signature = new FlowSignature();
            var signatures = new List<FlowSignature>();
            foreach (var result in data.MatchContents.Select(SignatureParser))
            {
                if (sort != result.Index)
                {
                    sort += 1;
                    signatures.Add(signature);
                    signature = new FlowSignature();
                }

                switch (result.Name)
                {
                    case "address":
                        signature.Address = new FlowAddress(result.Value);
                        break;
                    case "key_id":
                        signature.KeyId = Convert.ToUInt32(result.Value);
                        break;
                    case "signature":
                        signature.Signature = Encoding.UTF8.GetBytes(result.Value);
                        break;
                }
            }
            
            signatures.Add(signature);
            return signatures;
        }
        
        private (List<string> MatchContent, string RemainContent) CheckContent(string text, string keyword)
        {
            if (!text.ToLower().Contains(keyword))
            {
                return (new List<string>(), text);
            }

            var elements = text.Split("&").ToList();
            var matchElements = elements.Where(p => p.ToLower().Contains(keyword)).ToList();
            foreach (var element in matchElements)
            {
                elements.Remove(element);
            }
                
            return (matchElements, elements.Count > 0 ? string.Join("&", elements) : string.Empty);
        }
        
        private (int Index, string Name, string Value) AddressParser(string text)
        {
            var value = text.Split("=")[1];
            return (0, "address", value);
        }
        
        private (int Index, string Name, string Value) SignatureParser(string text)
        {
            var keyValue = text.Split("=");
            var propertiesPattern = @"(?<=\[)(.*)(?=\])";

            var match = Regex.Match(keyValue[0], propertiesPattern);
            if (!match.Success)
            {
                throw new Exception("App sdk return value format error");
            }

            var elements = match.Captures.FirstOrDefault()?.Value.Split("][");
            return (Convert.ToInt32(elements?[0]), elements?[1], keyValue[1]);
        }
        
        /// <summary>
        /// Get flow account
        /// </summary>
        /// <param name="address">address of account</param>
        /// <returns></returns>
        private async Task<FlowAccount> GetAccountAsync(string address)
        {
            var account = _flowClient.GetAccountAtLatestBlockAsync(address);
            return await account;
        }
        
        /// <summary>
        /// Get full account key information
        /// </summary>
        /// <param name="account">flow account</param>
        /// <param name="keyId">key id of account</param>
        /// <returns></returns>
        private FlowProposalKey GetProposerKey(FlowAccount account, uint keyId)
        {
            var proposalKey = account.Keys.First(p => p.Index == keyId);
            return new FlowProposalKey
                   {
                       Address = account.Address,
                       KeyId = keyId,
                       SequenceNumber = proposalKey.SequenceNumber
                   };
        }
    }
}