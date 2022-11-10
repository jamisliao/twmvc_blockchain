using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using twmcv_example.Client.Models;

namespace twmcv_example.Client.Pages;

public partial class Index
{
  [Inject] private HttpClient _http { get; set; }
  private string Url;
  private string AuthenticationId;

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();

    var result = await _http.GetFromJsonAsync<APIResult>("api/flow/login");
    Url = result.outPut.Result["Url"];
    AuthenticationId = result.outPut.Result["AuthenticationId"];
    
    StateHasChanged();
  }
}
