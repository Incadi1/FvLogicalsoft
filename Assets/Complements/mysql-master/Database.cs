using UnityEngine;
using Mirror;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Data;
using MySql.Data;								// From MySql.Data.dll in Plugins folder
using MySql.Data.MySqlClient;                   // From MySql.Data.dll in Plugins folder


using SqlParameter = MySql.Data.MySqlClient.MySqlParameter;


/// <summary>
/// Database class for mysql
/// Port of the sqlite database class from ummorpg
/// </summary>
public class Database : MonoBehaviour
{
    private static string connectionString = null;


    /// <summary>
    /// produces the connection string based on environment variables
    /// </summary>
    /// <value>The connection string</value>
     private static string ConnectionString
     {
         get
         {

             if (connectionString == null)
             {
                 var connectionStringBuilder = new MySqlConnectionStringBuilder
                 {
                     Server = GetEnv("MYSQL_HOST") ?? "localhost",
                     Database = GetEnv("MYSQL_DATABASE") ?? "virtualfair",
                     UserID = GetEnv("MYSQL_USER") ?? "root",
                     Password = GetEnv("MYSQL_PASSWORD") ?? "root",
                     Port = GetUIntEnv("MYSQL_PORT", 3307)
                     //CharacterSet = "utf8"
                 };
                 connectionString = connectionStringBuilder.ConnectionString;
             }

             return connectionString;
         }
     }

    private static void Transaction(Action<MySqlCommand> action)
    {
        using (var connection = new MySqlConnection(ConnectionString))
        {

            connection.Open();
            MySqlTransaction transaction = null;

            try
            {

                transaction = connection.BeginTransaction();

                MySqlCommand command = new MySqlCommand();
                command.Connection = connection;
                command.Transaction = transaction;

                action(command);

                transaction.Commit();

            }
            catch (Exception ex)
            {
                if (transaction != null)
                    transaction.Rollback();
                throw ex;
            }
        }
    }
    private static String GetEnv(String name)
      {
          return Environment.GetEnvironmentVariable(name);

      }

      private static uint GetUIntEnv(String name, uint defaultValue = 0)
      {
          var value = Environment.GetEnvironmentVariable(name);

          if (value == null)
              return defaultValue;

          uint result;

          if (uint.TryParse(value, out result))
              return result;

          return defaultValue;
      }
    private static void InitializeSchema()
     {


         ExecuteNonQueryMySql(@"
         CREATE TABLE IF NOT EXISTS accounts (
             name VARCHAR(16) NOT NULL,
             password CHAR(40) NOT NULL,
             banned BOOLEAN NOT NULL DEFAULT 0,
             PRIMARY KEY(name)
         ) ");

         ExecuteNonQueryMySql(@"
         CREATE TABLE IF NOT EXISTS characters(
             name VARCHAR(16) NOT NULL,
             account VARCHAR(16) NOT NULL,

             class VARCHAR(16) NOT NULL,
             x FLOAT NOT NULL,
             y FLOAT NOT NULL,
             z FLOAT NOT NULL,

             online TIMESTAMP,

             deleted BOOLEAN NOT NULL,

             PRIMARY KEY (name),
             INDEX(account),
             FOREIGN KEY(account)
                 REFERENCES accounts(name)
                 ON DELETE CASCADE ON UPDATE CASCADE
         )");
        // ) CHARACTER SET = utf8");

     }
    static Database()
    {
        Debug.Log("Initializing MySQL database");

        InitializeSchema();

        Utils.InvokeMany(typeof(Database), null, "Initialize_");
    }
    
    #region Helper Functions

      // run a query that doesn't return anything
      private static void ExecuteNonQueryMySql(string sql, params SqlParameter[] args)
      {
          try
          {
              MySqlHelper.ExecuteNonQuery(ConnectionString, sql, args);
          }
          catch (Exception ex)
          {
              Debug.LogErrorFormat("Failed to execute query {0}", sql);
              throw ex;
          }

      }


      private static void ExecuteNonQueryMySql(MySqlCommand command, string sql, params SqlParameter[] args)
      {
          try
          {
              command.CommandText = sql;
              command.Parameters.Clear();

              foreach (var arg in args)
              {
                  command.Parameters.Add(arg);
              }

              command.ExecuteNonQuery();
          }
          catch (Exception ex)
          {
              Debug.LogErrorFormat("Failed to execute query {0}", sql);
              throw ex;
          }

      }

      // run a query that returns a single value
      private static object ExecuteScalarMySql(string sql, params SqlParameter[] args)
      {
          try
          {
              return MySqlHelper.ExecuteScalar(ConnectionString, sql, args);
          }
          catch (Exception ex)
          {
              Debug.LogErrorFormat("Failed to execute query {0}", sql);
              throw ex;
          }
      }

      private static DataRow ExecuteDataRowMySql(string sql, params SqlParameter[] args)
      {
          try
          {
              return MySqlHelper.ExecuteDataRow(ConnectionString, sql, args);
          }
          catch (Exception ex)
          {
              Debug.LogErrorFormat("Failed to execute query {0}", sql);
              throw ex;
          }
      }

      private static DataSet ExecuteDataSetMySql(string sql, params SqlParameter[] args)
      {
          try
          {
              return MySqlHelper.ExecuteDataset(ConnectionString, sql, args);
          }
          catch (Exception ex)
          {
              Debug.LogErrorFormat("Failed to execute query {0}", sql);
              throw ex;
          }
      }
      // run a query that returns several values
      private static List<List<object>> ExecuteReaderMySql(string sql, params SqlParameter[] args)
      {
          try
          {
              var result = new List<List<object>>();

              using (var reader = MySqlHelper.ExecuteReader(ConnectionString, sql, args))
              {

                  while (reader.Read())
                  {
                      var buf = new object[reader.FieldCount];
                      reader.GetValues(buf);
                      result.Add(buf.ToList());
                  }
              }

              return result;
          }
          catch (Exception ex)
          {
              Debug.LogErrorFormat("Failed to execute query {0}", sql);
              throw ex;
          }

      }

      // run a query that returns several values
      private static MySqlDataReader GetReader(string sql, params SqlParameter[] args)
      {
          try
          {
              return MySqlHelper.ExecuteReader(ConnectionString, sql, args);
          }
          catch (Exception ex)
          {
              Debug.LogErrorFormat("Failed to execute query {0}", sql);
              throw ex;
          }
      }


#endregion


    // account data ////////////////////////////////////////////////////////////
    public static bool IsValidAccount(string account, string password)
    //public  bool IsValidAccount(string account, string password)
    {
        // this function can be used to verify account credentials in a database
        // or a content management system.
        //
        // for example, we could setup a content management system with a forum,
        // news, shop etc. and then use a simple HTTP-GET to check the account
        // info, for example:
        //
        //   var request = new WWW("example.com/verify.php?id="+id+"&amp;pw="+pw);
        //   while (!request.isDone)
        //       print("loading...");
        //   return request.error == null && request.text == "ok";
        //
        // where verify.php is a script like this one:
        //   <?php
        //   // id and pw set with HTTP-GET?
        //   if (isset($_GET['id']) && isset($_GET['pw'])) {
        //       // validate id and pw by using the CMS, for example in Drupal:
        //       if (user_authenticate($_GET['id'], $_GET['pw']))
        //           echo "ok";
        //       else
        //           echo "invalid id or pw";
        //   }
        //   ?>
        //
        // or we could check in a MYSQL database:
        //   var dbConn = new MySql.Data.MySqlClient.MySqlConnection("Persist Security Info=False;server=localhost;database=notas;uid=root;password=" + dbpwd);
        //   var cmd = dbConn.CreateCommand();
        //   cmd.CommandText = "SELECT id FROM accounts WHERE id='" + account + "' AND pw='" + password + "'";
        //   dbConn.Open();
        //   var reader = cmd.ExecuteReader();
        //   if (reader.Read())
        //       return reader.ToString() == account;
        //   return false;
        //
        // as usual, we will use the simplest solution possible:
        // create account if not exists, compare password otherwise.
        // no CMS communication necessary and good enough for an Indie MMORPG.

        // not empty?
        if (!string.IsNullOrWhiteSpace(account) && !string.IsNullOrWhiteSpace(password))
        {

            var row = ExecuteDataRowMySql("SELECT password, banned FROM accounts WHERE name=@name", new SqlParameter("@name", account));
            if (row != null)
            {
                return password == (string)row["password"] && !(bool)row["banned"];
            }
            else
            {
                // account doesn't exist. create it.
                ExecuteNonQueryMySql("INSERT INTO accounts VALUES (@name, @password, 0)", new SqlParameter("@name", account), new SqlParameter("@password", password));
                return true;
            }
        }
        return false;
    }

    // character data //////////////////////////////////////////////////////////
    public static bool CharacterExists(string characterName)
    {
        // checks deleted ones too so we don't end up with duplicates if we un-
        // delete one
        return ((long)ExecuteScalarMySql("SELECT Count(*) FROM characters WHERE name=@name", new SqlParameter("@name", characterName))) == 1;
    }

    public static void CharacterDelete(string characterName)
    {
        // soft delete the character so it can always be restored later
        ExecuteNonQueryMySql("UPDATE characters SET deleted=1 WHERE name=@character", new SqlParameter("@character", characterName));
    }

    // returns a dict of<character name, character class=prefab name>
    // we really need the prefab name too, so that client character selection
    // can read all kinds of properties like icons, stats, 3D models and not
    // just the character name
    public static List<string> CharactersForAccount(string account)
    {
        var result = new List<String>();

        var table = ExecuteReaderMySql("SELECT name FROM characters WHERE account=@account AND deleted=0", new SqlParameter("@account", account));
        foreach (var row in table)
            result.Add((string)row[0]);
        return result;
    }

     public static GameObject CharacterLoad(string characterName, List<Player> prefabs)
    {
        var row = ExecuteDataRowMySql("SELECT * FROM characters WHERE name=@name AND deleted=0", new SqlParameter("@name", characterName));
        if (row != null)
        {
            // instantiate based on the class name
            string className = (string)row["class"];
            var prefab = prefabs.Find(p => p.name == className);
            if (prefab != null)
            {
                var go = GameObject.Instantiate(prefab.gameObject);
                var player = go.GetComponent<Player>();

                player.name = (string)row["name"];
                player.account = (string)row["account"];
                player.className = (string)row["class"];
                float x = (float)row["x"];
                float y = (float)row["y"];
                float z = (float)row["z"];
                Vector3 position = new Vector3(x, y, z);
               
                // addon system hooks
                Utils.InvokeMany(typeof(Database), null, "CharacterLoad_", player);

                return go;
            }
            else Debug.LogError("no prefab found for class: " + className);
        }
        return null;
    }


    // adds or overwrites character data in the database
    static void CharacterSave(Player player, bool online, MySqlCommand command)
    {
        // online status:
        //   '' if offline (if just logging out etc.)
        //   current time otherwise
        // -> this way it's fault tolerant because external applications can
        //    check if online != '' and if time difference < saveinterval
        // -> online time is useful for network zones (server<->server online
        //    checks), external websites which render dynamic maps, etc.
        // -> it uses the ISO 8601 standard format
        DateTime? onlineTimestamp = null;

        if (!online)
            onlineTimestamp = DateTime.Now;

        var query = @"
            INSERT INTO characters 
            SET
                name=@name,
                account=@account,
                class = @class,
                x = @x,
                y = @y,
                z = @z,
                online = @online,
                deleted = 0,
            ON DUPLICATE KEY UPDATE 
                account=@account,
                class = @class,
                x = @x,
                y = @y,
                z = @z,
                online = @online,
                deleted = 0,
            ";

        ExecuteNonQueryMySql(command, query,
                    new SqlParameter("@name", player.name),
                    new SqlParameter("@account", player.account),
                    new SqlParameter("@class", player.className),
                    new SqlParameter("@x", player.transform.position.x),
                    new SqlParameter("@y", player.transform.position.y),
                    new SqlParameter("@z", player.transform.position.z),
                    new SqlParameter("@online", onlineTimestamp)
                       );

        // addon system hooks
        Utils.InvokeMany(typeof(Database), null, "CharacterSave_", player);
    }

    // adds or overwrites character data in the database
    public static void CharacterSave(Player player, bool online, bool useTransaction = true)
    {
        // only use a transaction if not called within SaveMany transaction
        Transaction(command =>
        {
            CharacterSave(player, online, command);
        });
    }

    // save multiple characters at once (useful for ultra fast transactions)
   
   // public static void CharacterSaveMany(List<Player> players, bool online = true)
    public static void CharacterSaveMany(IEnumerable<Player> players, bool online = true)
    {
        Transaction(command =>
        {
            foreach (var player in players)
                CharacterSave(player, online, command);
        });
    }


   
}
