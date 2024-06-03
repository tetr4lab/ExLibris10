using System.Data;
using System.Data.Common;
using PetaPoco.Core;

namespace PetaPoco;

public class MySqlDatabase : Database {
    public MySqlDatabase (IDatabaseBuildConfiguration configuration) : base (configuration) { }
    public MySqlDatabase (IDbConnection connection, IMapper? defaultMapper = null) : base (connection, defaultMapper) { }
    public MySqlDatabase (string connectionString, string providerName, IMapper? defaultMapper = null) : base (connectionString, providerName, defaultMapper) { }
    public MySqlDatabase (string connectionString, DbProviderFactory factory, IMapper? defaultMapper = null) : base (connectionString, factory, defaultMapper) { }
    public MySqlDatabase (string connectionString, IProvider provider, IMapper? defaultMapper = null) : base (connectionString, provider, defaultMapper) { }

    public override bool OnException (Exception ex) {
        System.Diagnostics.Debug.WriteLine ($"Database.OnException: {LastCommand}\n{ex}");
        return base.OnException (ex);
    }
}
