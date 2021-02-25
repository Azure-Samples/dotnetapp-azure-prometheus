using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;

namespace aspnet_core_dotnet_core.Pages
{
    public class CrashModel : PageModel
    {
        IHostApplicationLifetime AppLifetime; 
        public CrashModel(IHostApplicationLifetime applifetime)
        {
            AppLifetime = applifetime;
        }
        public void OnGet()
        {
            AppLifetime.StopApplication();
        }
    }
}