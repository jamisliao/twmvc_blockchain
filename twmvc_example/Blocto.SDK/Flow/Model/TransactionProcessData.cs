using Flow.FCL.Models.Authz;
using Flow.Net.Sdk.Core.Models;
using Newtonsoft.Json.Linq;

namespace Blocto.Sdk.Flow.Model;

public class TransactionProcessData
{
    public PreAuthzAdapterResponse PreAuthzAdapterResponse { get; set; }

    public FlowTransaction Transaction { get; set; }

    public string PollingUrl { get; set; }

    public JObject SignableObj { get; set; }
}