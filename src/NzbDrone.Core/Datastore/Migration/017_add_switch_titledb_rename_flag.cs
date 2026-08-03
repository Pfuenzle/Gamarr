using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(17)]
    public class add_switch_titledb_rename_flag : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("NamingConfig")
                 .AddColumn("EnableSwitchTitleDbRename")
                 .AsBoolean()
                 .NotNullable()
                 .WithDefaultValue(false);
        }
    }
}
