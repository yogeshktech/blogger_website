namespace Blogger_website.Models.DatabaseLayer
{
    public partial interface IDatabaseLayer
    {

    }

    public partial class DatabaseLayer : IDatabaseLayer
    {
        private readonly IConfiguration _configuration;
        private readonly string DbConnection;
        public DatabaseLayer(IConfiguration configuration)
        {
            this._configuration = configuration;
            this.DbConnection = this._configuration.GetConnectionString("AppDbContextConnection");
        }
    }
}
