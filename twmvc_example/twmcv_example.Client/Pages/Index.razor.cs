using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using twmcv_example.Client.Models;

namespace twmcv_example.Client.Pages;

public partial class Index
{
  [Inject] private HttpClient _http { get; set; }
  private string Url;
  private string AuthenticationId;
  private string Address;
  private Timer checkTimer;
  private string ReceiveAddress;
  private int TransferAmount;
  private string TransactionAuthId;
  private string TransactionSessionId;
  private string TransactionId;
  private string TransactionResultUrl;

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();

    var result = await _http.GetFromJsonAsync<APIResult>("api/flow/login");
    Url = result.outPut.Result["Url"];
    AuthenticationId = result.outPut.Result["AuthenticationId"];
    
    StateHasChanged();
    
    checkTimer = new Timer(CheckAuth, null, 0, 1000);
  }

  private async void CheckAuth(object? state)
  {
    try
    {
      var result =
        await _http.GetFromJsonAsync<APIResult>($"api/flow/login/result?authenticationId={AuthenticationId}");
      if (result.outPut.ErrorCode != 0)
      {
        return;
      }

      await checkTimer.DisposeAsync();
      Url = "";
      Address = result.outPut.Result["Address"];
      await InvokeAsync(StateHasChanged);
    }
    catch (Exception e)
    {
      Console.WriteLine(e.ToString());
    }
  }

  private async void Mutate()
  {
    try
    {
      TransactionResultUrl = "";
      Url = "";
      
      var response =
        await _http.PostAsync($"api/flow/transaction/{Address}/to/{ReceiveAddress}/{TransferAmount}", null);
      var result = await response.Content.ReadFromJsonAsync<APIResult>();
      Url = result.outPut.Result["Url"];
      TransactionAuthId = result.outPut.Result["AuthorizationId"];
      TransactionSessionId = result.outPut.Result["SessionId"];
      
      StateHasChanged();

      checkTimer = new Timer(CheckTransaction, null, 0, 1000);
    }
    catch (Exception e)
    {
      Console.WriteLine(e.ToString());
    }
  }

  private async void CheckTransaction(object? state)
  {
    try
    {
      var response =
        await _http.PostAsync($"api/flow/transaction/{TransactionAuthId}/{TransactionSessionId}", null);
      var result = await response.Content.ReadFromJsonAsync<APIResult>();
      if (result.outPut.ErrorCode != 0)
      {
        return;
      }

      await checkTimer.DisposeAsync();
      Url = "";
      TransactionId = result.outPut.Result["txId"];
      TransactionResultUrl = result.outPut.Result["FlowScanUrl"];
      await InvokeAsync(StateHasChanged);
    }
    catch (Exception e)
    {
      Console.WriteLine(e.ToString());
    }
  }
}
