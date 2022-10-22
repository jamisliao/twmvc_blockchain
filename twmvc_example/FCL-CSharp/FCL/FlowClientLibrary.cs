using Flow.FCL.Models;
using Flow.FCL.WalletProvider;
using Flow.Net.Sdk.Core;
using Flow.Net.Sdk.Core.Cadence;
using Flow.Net.Sdk.Core.Client;
using Flow.Net.Sdk.Core.Models;

namespace Flow.FCL
{
    public class FlowClientLibrary 
    {
        public static Config.Config Config { get; private set; }
        
        public IFlowClient FlowClient { get; private set; }
        
        private string _script = "import FungibleToken from 0x9a0766d93b6608b7\nimport FlowToken from 0x7e60df042a9c0868\n\ntransaction(amount: UFix64, to: Address) {\n\n    // The Vault resource that holds the tokens that are being transferred\n    let sentVault: @FungibleToken.Vault\n\n    prepare(signer: AuthAccount) {\n\n        // Get a reference to the signer's stored vault\n        let vaultRef = signer.borrow<&FlowToken.Vault>(from: /storage/flowTokenVault)\n            ?? panic(\"Could not borrow reference to the owner's Vault!\")\n\n        // Withdraw tokens from the signer's stored vault\n        self.sentVault <- vaultRef.withdraw(amount: amount)\n    }\n\n    execute {\n\n        // Get the recipient's public account object\n        let recipient = getAccount(to)\n\n        // Get a reference to the recipient's Receiver\n        let receiverRef = recipient.getCapability(/public/flowTokenReceiver)\n            .borrow<&{FungibleToken.Receiver}>()\n            ?? panic(\"Could not borrow receiver reference to the recipient's Vault\")\n\n        // Deposit the withdrawn tokens in the recipient's receiver\n        receiverRef.deposit(from: <-self.sentVault)\n    }\n}";
    
        private string _queryScript = @"
            import ValueDapp from 0x5a8143da8058740c

            pub fun main(): UFix64 {
                return ValueDapp.value
            }";
    
        private string _mutateScript = @"
            import ValueDapp from 0x5a8143da8058740c

            transaction(value: UFix64) {
                prepare(authorizer: AuthAccount) {
                    ValueDapp.setValue(value)
                }
            }";
        
        private CurrentUser _currentUser;
        
        private Transaction _transaction;
        
        private ICadence _response;
        
        private IWalletProvider _walletProvider;
        
        private string _errorMessage;
        
        private bool _isSuccessed;
        
        static FlowClientLibrary()
        {
            Config = new Config.Config();
        }
        
        public static void SetConfig(Config.Config config)
        {
            Config = config;
        }
        
        public FlowClientLibrary(IWalletProvider walletProvider, Transaction transaction, CurrentUser currentUser, IFlowClient flowClient)
        {
            FlowClient = flowClient;
            
           _response = default(Cadence);
           _errorMessage = string.Empty;
           _isSuccessed = false;
           
           _walletProvider = walletProvider;
           _transaction = transaction;
           _currentUser = currentUser;
        }

        /// <summary>
        /// Get CurrentUser
        /// </summary>
        /// <returns>CurrentUser</returns>
        public CurrentUser CurrentUser()
        {
            return _currentUser;
        }
        
        /// <summary>
        /// Calling this method will authenticate the current user via any wallet that supports FCL.
        /// Once called, FCL will initiate communication with the configured discovery.wallet endpoint which lets the user select a wallet to authenticate with.
        /// Once the wallet provider has authenticated the user,
        /// FCL will set the values on the current user object for future use and authorization.
        /// </summary>
        /// <param name="callback">The callback will be called when the user authenticates and un-authenticates, making it easy to update the UI accordingly.</param>
        public void AuthenticateAsync(Action<CurrentUser, AccountProofData> callback = null)
        {
            var url = Config.Get("discovery.wallet");
            _currentUser.AuthenticateAsync(url);
        }
        
        /// <summary>
        /// Calling this method will authenticate the current user via any wallet that supports FCL.
        /// Once called, FCL will initiate communication with the configured discovery.wallet endpoint which lets the user select a wallet to authenticate with.
        /// Once the wallet provider has authenticated the user,
        /// FCL will set the values on the current user object for future use and authorization.
        /// </summary>
        /// <param name="accountProofData">Account proof data</param>
        /// <param name="callback">The callback will be called when the user authenticates and un-authenticates, making it easy to update the UI accordingly.</param>
        public async Task<(string Url, string AuthenticationId)> AuthenticateAsync(AccountProofData accountProofData, Action<CurrentUser, AccountProofData> callback = null!)
        {
            var url = Config.Get("discovery.wallet");
            var data = await _currentUser.AuthenticateAsync(url, accountProofData);
            return data;
        }
        
        public async Task<string> AuthenticateResultAsync(string authenticationId)
        {
            var address = await _currentUser.AuthenticateResultAsync(authenticationId);
            return address;
        }
        
        /// <summary>
        /// Logs out the current user and sets the values on the current user object to null.
        /// </summary>
        /// <param name="callback">The callback will be called when the user authenticates and un-authenticates, making it easy to update the UI accordingly.</param>
        public void UnAuthenticate(Action callback = null)
        {
            this._currentUser.Services = null;
            this._currentUser.LoggedIn = false;
            this._currentUser.ExpiresAt = default;
            callback?.Invoke();
        }
        
        /// <summary>
        /// Login
        /// </summary>
        /// <param name="callback">The callback will be called when the user authenticates and un-authenticates, making it easy to update the UI accordingly.</param>
        public void Login(Action<CurrentUser, AccountProofData> callback = null)
        {
            AuthenticateAsync(callback);
        }
        
        /// <summary>
        /// ReLogin
        /// </summary>
        /// <param name="callback">The callback will be called when the user authenticates and un-authenticates, making it easy to update the UI accordingly.</param>
        public void ReLogin(Action<CurrentUser, AccountProofData> callback = null)
        {
            UnAuthenticate();
            AuthenticateAsync(callback);
        }
        
        /// <summary>
        /// As the current user Mutate the Flow Blockchain
        /// </summary>
        /// <param name="tx">FlowTransaction</param>
        /// <param name="callback">The callback will be called when the send transaction completed, making it easy to update the UI accordingly.</param>
        public async Task<(string Url, string AuthorizationId, string SessionId)> MutateAsync(string address, FlowTransaction tx, Action<string> callback = null)
        {
            var service = Models.CurrentUser.FclServiceMapper[address].FirstOrDefault(p => p.Type == ServiceTypeEnum.PREAUTHZ);
            if(service is null)
            {
                throw new Exception("Please connect wallet first.");
                
            }
            
            tx.Script = _script;
            tx.GasLimit = 1000;
            var result = await _transaction.SendTransaction(service, tx, callback);
            return result;
        }
        
        public async Task<string> MutateResultAsync(string authorizationId, string sessionId)
        {
            var result = await _transaction.SendTransactionResultAsync(authorizationId, sessionId);
            return result;
        }
        
        public async Task<FlowTransactionResult> MetateExecuteResultAsync(string txId)
        {
            var result = await FlowClient.GetTransactionResultAsync(txId);
            return result;
        }
        
        /// <summary>
        /// Sign user message
        /// </summary>
        /// <param name="message">Source message</param>
        /// <param name="callback">Complete sign message then call callback function</param>
        public async Task<(string Url, string SignatureId)> SignUserMessage(string address, string message)
        {
            var data = await _currentUser.SignUserMessage(address, message);
            return data;
        }
        
        public async Task<List<FlowSignature>> SignUserMessageResultAsync(string signatureId)
        {
            var flowSignatures = await _currentUser.SignUserMessageResultAsync(signatureId);
            return flowSignatures;
        }
        
        /// <summary>
        /// Query the Flow Blockchain
        /// </summary>
        /// <param name="flowScript">Flow cadence script and arguments</param>
        /// <returns>QueryResult</returns>
        public async Task<ExecuteResult<ICadence>> QueryAsync(FlowScript flowScript)
        {
            await ExecuteScript(flowScript);
            var result = new ExecuteResult<ICadence>
                         {
                             Data = _response,
                             IsSuccessed = _isSuccessed,
                             Message = _errorMessage
                         };
            _isSuccessed = false;
            _errorMessage = string.Empty;
            _response = default(Cadence);
            return result;
        }
        
        /// <summary>
        /// Get transaction result
        /// </summary>
        /// <param name="transactionId">Transaction hash code</param>
        /// <returns>Tuple include Execution, Status, BlockId</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public ExecuteResult<FlowTransactionResult> GetTransactionStatus(string transactionId)
        {
            var txr = _transaction.GetTransactionStatus(transactionId);
            var result = txr.Execution switch
                         {
                             TransactionExecution.Failure => new ExecuteResult<FlowTransactionResult>
                                                             {
                                                                 Data = txr,
                                                                 IsSuccessed = true,
                                                                 Message = txr.ErrorMessage
                                                             },
                             TransactionExecution.Success => new ExecuteResult<FlowTransactionResult>
                                                             {
                                                                 Data = txr,
                                                                 IsSuccessed = true,
                                                                 Message = string.Empty
                                                             },
                             TransactionExecution.Pending => new ExecuteResult<FlowTransactionResult>
                                                             {
                                                                 Data = txr,
                                                                 IsSuccessed = true,
                                                                 Message = "Still Pending"
                                                             },
                             _ => throw new ArgumentOutOfRangeException()
                         }; 
            
            return result;
        }
        
        private async Task ExecuteScript(FlowScript flowScript)
        {
            try
            {
                _response = await FlowClient.ExecuteScriptAtLatestBlockAsync(flowScript); 
                _isSuccessed = true;
            }
            catch (Exception e)
            {
                _errorMessage = e.Message;
            }
        }
    }
}