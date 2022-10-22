using System.Text;
using Flow.FCL.Extensions;
using Flow.FCL.Utility;
using Flow.FCL.WalletProvider;
using Flow.Net.Sdk.Core;
using Flow.Net.Sdk.Core.Models;

namespace Flow.FCL.Models
{
    public class CurrentUser : User
    {
        public static string Test;
        public static Dictionary<string, List<FclService>> FclServiceMapper = new Dictionary<string, List<FclService>>();
        
        public CurrentUser(IWalletProvider walletProvider, AppUtility appUtility)
        {
            LoggedIn = false;
            Services = new List<FclService>();
            _walletProvider = walletProvider;
            _appUtility = appUtility;
            
            Addr = new FlowAddress("0x");
        }
        
        public List<FclService> Services { get; set; }

        private AccountProofData AccountProofData { get; set; }

        private readonly IWalletProvider _walletProvider;
        
        private readonly AppUtility _appUtility;
        
        private static Dictionary<string, AccountProofData> accountProofDataStore = new Dictionary<string, AccountProofData>();

        /// <summary>
        /// Returns the current user object.
        /// </summary>
        /// <returns>CurrentUser</returns>
        public CurrentUser Snapshot()
        {
            return this;
        }

        /// <summary>
        /// Calling this method will authenticate the current user via any wallet that supports FCL.
        /// Once called, FCL will initiate communication with the configured discovery.wallet endpoint which lets the user select a wallet to authenticate with.
        /// Once the wallet provider has authenticated the user,
        /// FCL will set the values on the current user object for future use and authorization.
        /// </summary>
        /// <param name="url">Authn url</param>
        /// <param name="callback">The callback will be called when the user authenticates and un-authenticates, making it easy to update the UI accordingly.</param>
        public void AuthenticateAsync(string url)
        {
            AuthenticateAsync(url, null);
        }

        /// <summary>
        /// Calling this method will authenticate the current user via any wallet that supports FCL.
        /// Once called, FCL will initiate communication with the configured discovery.wallet endpoint which lets the user select a wallet to authenticate with.
        /// Once the wallet provider has authenticated the user,
        /// FCL will set the values on the current user object for future use and authorization.
        /// </summary>
        /// <param name="url">Authn url</param>
        /// <param name="accountProofData">Flow account proof data</param>
        /// <param name="callback">The callback will be called when the user authenticates and un-authenticates, making it easy to update the UI accordingly.</param>
        public async Task<(string Url, string AuthenticationId)> AuthenticateAsync(string url, AccountProofData accountProofData = null)
        {
            var parameters = new Dictionary<string, object>();
            if (accountProofData != null)
            {
                parameters =
                    new Dictionary<string, object> {
                        { "accountProofIdentifier", accountProofData.AppId },
                        { "accountProofNonce", accountProofData.Nonce }
                    };
            }
            
            var data = await _walletProvider.AuthenticateAsync(url, parameters) ;
            
            if(accountProofData != null)
            {
                CurrentUser.accountProofDataStore.Add(data.AuthenticationId, accountProofData);
            }
            
            return data;
        }
        
        public async Task<string> AuthenticateResultAsync(string authenticationId)
        {
            await _walletProvider.AuthenticateResultAsync(authenticationId, item => {
                                                                                          var response = item as AuthenticateResponse;
                                                                                          switch (response?.ResponseStatus)
                                                                                          {
                                                                                              case ResponseStatusEnum.APPROVED:
                                                                                                  Addr = new FlowAddress(response.Data.Addr); 
                                                                                                  LoggedIn = true;
                                                                                                  F_type = "USER";
                                                                                                  F_vsn = response.FVsn;
                                                                                                  Services = response.Data.Services.ToList();
                                                                                                  ExpiresAt = response.Data.Expires;
                                                                                                  ExpiresAt = response.Data.Expires;
                                                                                                  if(CurrentUser.FclServiceMapper.ContainsKey(Addr.Address) == false)
                                                                                                  {
                                                                                                      CurrentUser.FclServiceMapper.Add(Addr.Address, Services);
                                                                                                  }
                                                                                                  
                                                                                                  break;
                                                                                              case ResponseStatusEnum.DECLINED:
                                                                                                  LoggedIn = false;
                                                                                                  F_type = "USER";
                                                                                                  F_vsn = response.FVsn;
                                                                                                  break;
                                                                                              case ResponseStatusEnum.PENDING:
                                                                                                  return;
                                                                                              case ResponseStatusEnum.REDIRECT:
                                                                                              case ResponseStatusEnum.NONE:
                                                                                              default:
                                                                                                  break;
                                                                                          }
                                                                                          
                                                                                          var accountProofService = Services.FirstOrDefault(p => p.Type == ServiceTypeEnum.AccountProof);
                                                                                          if (accountProofService == null)
                                                                                          {
                                                                                              return;
                                                                                          }

                                                                                          var accountProofData = CurrentUser.accountProofDataStore[authenticationId];
                                                                                          foreach (var signature in accountProofService.Data.Signatures!.Where(signature => !accountProofData.Signature.Any(p => p.KeyId ==  Convert.ToUInt32(signature.KeyId()))))
                                                                                          {
                                                                                              accountProofData.Signature.Add(new FlowSignature()
                                                                                                                             {
                                                                                                                                 Address = new FlowAddress(accountProofService.Data.Address),
                                                                                                                                 KeyId = Convert.ToUInt32(signature.KeyId()),
                                                                                                                                 Signature = Encoding.UTF8.GetBytes(signature.SignatureStr())
                                                                                                                             });
                                                                                          }
                                                                                          var isVerify = _appUtility.VerifyAccountProofSignature(accountProofData.AppId, accountProofData, "0x5b250a8a85b44a67");
                                                                                          if(isVerify == false)
                                                                                          {
                                                                                              throw new Exception("AccountProofData not verify.");
                                                                                          }
                                                                                      });
            return Addr.Address;
        }
        
        public async Task<(string Url, string SignatureId)> SignUserMessage(string address, string message)
        {
            var service = Models.CurrentUser.FclServiceMapper[address.RemoveHexPrefix()].FirstOrDefault(p => p.Type == ServiceTypeEnum.USERSIGNATURE);
            if (service is null)
            {
                throw new Exception("Please connect wallet first.");
            }

            var url = await _walletProvider.SignMessageAsync(message, service);
            return url;
        }
        
        public async Task<List<FlowSignature>> SignUserMessageResultAsync(string signatureId)
        {
            var flowSignatures = await _walletProvider.SignMessageResultAsync(signatureId);
            return flowSignatures;
        }
        
        private void ApproveProcess(AuthenticateResponse response)
        {
            Addr = new FlowAddress(response.Data.Addr);
            LoggedIn = true;
            F_type = "USER";
            F_vsn = response.FVsn;
            Services = response.Data.Services.ToList();
            ExpiresAt = response.Data.Expires;
            ExpiresAt = response.Data.Expires;
        }
    }
}
