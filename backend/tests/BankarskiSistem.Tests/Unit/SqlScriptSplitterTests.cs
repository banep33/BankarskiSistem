using BankarskiSistem.Tests.Integration;

namespace BankarskiSistem.Tests.Unit
{
    public class SqlScriptSplitterTests
    {
        [Fact]
        public void SplitScript_HandlesCustomDelimiterBlocks()
        {
            var script = """
                CREATE TABLE a (Id int);
                DELIMITER $$
                CREATE TRIGGER t BEFORE DELETE ON a FOR EACH ROW
                BEGIN
                  SIGNAL SQLSTATE '45000';
                END$$
                DELIMITER ;
                INSERT INTO a VALUES (1);
                """;

            var statements = MySqlDatabaseFixture.SplitScript(script).ToList();

            Assert.Equal(3, statements.Count);
            Assert.StartsWith("CREATE TRIGGER", statements[1]);
            Assert.EndsWith("END", statements[1]);
        }
    }
}
