using Blocto.Sdk.Core.Utility;
using Flow.FCL;
using Flow.FCL.Models;
using Flow.Net.Sdk.Core;
using Flow.Net.Sdk.Core.Cadence;
using Flow.Net.Sdk.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using twmvc_example.Extensions;
using twmvc_example.Models;

namespace twmvc_example.Controllers;

[ApiController]
[Route("[controller]")]
public class FlowController : ControllerBase
{
    private ILogger<FlowController> _logger;
    
    private FlowClientLibrary _fcl;
    
    public FlowController(FlowClientLibrary fcl, ILogger<FlowController> logger) 
    {
        _fcl = fcl;
        _logger = logger;
    }
    
    [HttpGet]
    [Route("login")]
    public async Task<ApiResult> Login()
    {
        _logger.LogInformation("Test serilog.");
        var accountProofData = new AccountProofData
                               {
                                   AppId = "com.blocto.flow.unitydemo",
                                   Nonce = KeyGenerator.GetUniqueKey(32).StringToHex()
                               };
        var data = await _fcl.AuthenticateAsync(accountProofData);
        var result = new Dictionary<string, string>
                     {
                         { "Url", data.Url },
                         { "AuthenticationId", data.AuthenticationId }
                     };
        
        return new ApiResult(ResultCodeEnum.Success, result);
    }
    
    [HttpGet]
    [Route("login/Result")]
    public async Task<ApiResult> Login(string authenticationId)
    {
        _logger.LogInformation($"AuthenticationId: {authenticationId}");
        var address = await _fcl.AuthenticateResultAsync(authenticationId);
        
        var apiResult = default(ApiResult);
        if(address == "")
        {
            apiResult = new ApiResult(ResultCodeEnum.StillPending, new object(), ResultCodeEnum.StillPending.GetEnumDescription());
        }
        else
        {
            apiResult = new ApiResult(ResultCodeEnum.Success, new Dictionary<string, string>
                                                              {
                                                                  { "Address", address.AddHexPrefix()}
                                                              });
        }
        
        return apiResult;
    }
    
    [HttpPost]
    [Route("transaction/{address}/to/{receiveAddress}/{value}")]
    public async Task<ApiResult> Mutate(string address, string receiveAddress, decimal value)
    {
        _logger.LogInformation($"Address: {address}, ReceivceAddress: {receiveAddress}, Value: {value}");
        var transaction = new FlowTransaction
                          {
                              Arguments = new List<ICadence>
                                          {
                                              new CadenceNumber(CadenceNumberType.UFix64, $"{value:N8}"),
                                              new CadenceAddress(receiveAddress.AddHexPrefix())
                                          }
                          };
        var data = await _fcl.MutateAsync(address.RemoveHexPrefix(), transaction);
        
        return new ApiResult(ResultCodeEnum.Success, new Dictionary<string, string>
                                                     {
                                                         { "Url", data.Url },
                                                         { "AuthorizationId", data.AuthorizationId },
                                                         { "SessionId", data.SessionId }
                                                     });
    }
    
    [HttpPost]
    [Route("transaction/{authorizationId}/{sessionId}")]
    public async Task<ApiResult> Mutate(string authorizationId, string sessionId)
    {
        _logger.LogInformation($"AuthorizationId: {authorizationId}, SessionId: {sessionId}");
        var data = await _fcl.MutateResultAsync(authorizationId, sessionId);
        
        return new ApiResult(ResultCodeEnum.Success, new Dictionary<string, string>
                                                     {
                                                         { "txId", data },
                                                         { "FlowScanUrl", $"https://testnet.flowscan.org/transaction/{data}" }
                                                     });
    }
    
    [HttpPost]
    [Route("signmessage/{address}/{message}")]
    public async Task<ApiResult> SignMessage(string address, string message)
    {
        _logger.LogInformation($"Message: {message}");
        var data = await _fcl.SignUserMessage(address, message);
        return new ApiResult(ResultCodeEnum.Success, new Dictionary<string, string>
                                                     {
                                                         { "Url", data.Url },
                                                         { "SignatureId", data.SignatureId }
                                                     });
    }
    
    [HttpGet]
    [Route("signmessage/Result/{signatureId}")]
    public async Task<ApiResult> SignMessageResult(string signatureId)
    {
        _logger.LogInformation($"SignatureId: {signatureId}");
        var data = await _fcl.SignUserMessageResultAsync(signatureId);
        return new ApiResult(ResultCodeEnum.Success, data);
    }
    
    [HttpGet]
    [Route("transaction/result/{txId}")]
    public async Task<ApiResult> GetTxResult(string txId)
    {
        _logger.LogInformation($"TxId: {txId}");
        var result = await _fcl.MetateExecuteResultAsync(txId);
        return new ApiResult(ResultCodeEnum.Success, result);
    }
}