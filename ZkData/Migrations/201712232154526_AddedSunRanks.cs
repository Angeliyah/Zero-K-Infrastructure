namespace ZkData.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddedSunRanks : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Accounts", "Rank", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Accounts", "Rank");
        }
    }
}
