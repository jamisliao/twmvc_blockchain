using Flow.FCL.Models;
using Flow.FCL.Utility;
using Flow.FCL.WalletProvider;
using Flow.Net.Sdk.Core.Client;
using Flow.Net.Sdk.Core.Models;

namespace Flow.FCL
{
    public class Transaction
    {
        private IWalletProvider _walletProvider;

        private IResolveUtility _resolveUtility;

        private IFlowClient _flowClient;

        private string _txId;

        public Transaction(IWalletProvider walletProvider, IFlowClient flowClient)
        {
            _walletProvider = walletProvider;
            _flowClient = flowClient;
        }

        public virtual async Task<(string Url, string AuthorizationId, string SessionId)> SendTransaction(FclService service, FlowTransaction tx, Action<string> callback = null)
        {
            var lastBlock = _flowClient.GetLatestBlockAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            tx.ReferenceBlockId = lastBlock.Header.Id;
            var result = await _walletProvider.SendTransaction(service, tx);
            return result;
        }
        
        public virtual async Task<string> SendTransactionResultAsync(string authorizationId, string sessionId)
        {
            var txId = await _walletProvider.SendTransactionResultAsync(authorizationId, sessionId);
            return txId;
        }

        public FlowTransactionResult GetTransactionStatus(string transactionId)
        {
            var txr = _flowClient.GetTransactionResultAsync(transactionId).ConfigureAwait(false).GetAwaiter().GetResult();
            return txr;
        }

        public async Task<FlowAccount> GetAccount(string address)
        {
            var account = _flowClient.GetAccountAtLatestBlockAsync(address);
            return await account;
        }

        private FlowProposalKey GetProposerKey(FlowAccount account, uint keyId)
        {
            var proposalKey = account.Keys.First(p => p.Index == keyId);
            return new FlowProposalKey {
                Address = account.Address,
                KeyId = keyId,
                SequenceNumber = proposalKey.SequenceNumber
            };
        }
    }
}
