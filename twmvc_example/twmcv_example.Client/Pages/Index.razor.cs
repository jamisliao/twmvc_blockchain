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
  private Timer checkAuthTimer;

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();

    var result = await _http.GetFromJsonAsync<APIResult>("api/flow/login");
    Url = result.outPut.Result["Url"];
    AuthenticationId = result.outPut.Result["AuthenticationId"];
    
    StateHasChanged();
    
    checkAuthTimer = new Timer(CheckAuth, null, 0, 1000);
  }

  private async void CheckAuth(object? state)
  {
    var result = await _http.GetFromJsonAsync<APIResult>($"api/flow/login/result?authenticationId={AuthenticationId}");
    if (result.outPut.ErrorCode != 0)
    {
      return;
    }

    await checkAuthTimer.DisposeAsync();
    Url = "";
    Address = result.outPut.Result["Address"];
    await InvokeAsync(StateHasChanged);
  }
}
