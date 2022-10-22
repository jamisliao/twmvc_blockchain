using Flow.FCL.Models;
using Flow.FCL.Models.Authz;
using Flow.Net.Sdk.Core.Models;

namespace Flow.FCL.WalletProvider
{
    public interface IWalletProvider
    {
        /// <summary>
        /// User connect wallet get account
        /// </summary>
        /// <param name="url">fcl authn url</param>
        /// <param name="parameters">parameter of authn</param>
        /// <param name="internalCallback">After, get endpoint response internal callback.</param>
        public Task<(string Url, string AuthenticationId)> AuthenticateAsync(string url, Dictionary<string, object> parameters, Action<object> internalCallback = null);
        
        public Task AuthenticateResultAsync(string authenticationId, Action<object> internalCallback = null!);
        /// <summary>
        /// Send Transaction
        /// </summary>
        /// <param name="service">fcl preauth service</param>
        /// <param name="tx">flow transaction</param>
        /// <param name="internalCallback">completed transaction callback</param>
        public Task<(string Url, string AuthorizationId, string SessionId)> SendTransaction(FclService service, FlowTransaction tx, Action<string> internalCallback = null);
        
        public Task<string> SendTransactionResultAsync(string authorizationId, string sessionId);

        /// <summary>
        /// SignMessage
        /// </summary>
        /// <param name="message">Original message </param>
        /// <param name="signService">FCL signature service</param>
        public Task<(string Url, string SignatureId)> SignMessageAsync(string message, FclService signService);
        public Task<List<FlowSignature>> SignMessageResultAsync(string signatureId);
    }
}