using Blogger_website.Areas.Identity.Data;
using Blogger_website.Models.DatabaseLayer;
using Blogger_website.Services;
using Microsoft.AspNetCore.Identity;

namespace Blogger_website.Models.BusinessLayer
{
    public partial interface IBusinessLayer
    {

    }

    public partial class BusinessLayer : IBusinessLayer
    {
        private IWebHostEnvironment _env;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private UserManager<ApplicationUser> _userManager;
        private readonly IDatabaseLayer _databaseLayer;
        private readonly IAdminRegistrationService _registrationService;
        private readonly IEmailNotificationService _emailNotificationService;
        private readonly IHttpContextAccessor _httpContextAccessor;


        public BusinessLayer(
            IWebHostEnvironment env, IServiceScopeFactory serviceScopeFactory, IConfiguration configuration,
             IDatabaseLayer dataBaseLayer,
            UserManager<ApplicationUser> userManager,
            IAdminRegistrationService registrationService,
            IEmailNotificationService emailNotificationService,
            IHttpContextAccessor httpContextAccessor
            )
        {
            this._env = env;
            this._scopeFactory = serviceScopeFactory;
            this._configuration = configuration;
            this._userManager = userManager;
            this._databaseLayer = dataBaseLayer;
            _registrationService = registrationService;
            _emailNotificationService = emailNotificationService;
            _httpContextAccessor = httpContextAccessor;
        }


    }
}
