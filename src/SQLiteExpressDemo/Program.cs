using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;

// Works on the following Nuget Packages:
// Stub.System.Data.SQLite.Core.NetFramework
// Stub.System.Data.SQLite.Core.NetStandard

namespace SQLiteExpressDemo
{
    internal class Program
    {
        // The demo writes a fresh .db file into the exe folder on every run.
        static readonly string DbPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "demo.db");

        // A second db file is used for the AttachDatabase demo.
        static readonly string AttachDbPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "demo_attach.db");

        static void Main(string[] args)
        {
            // Start from a clean slate so every run is reproducible.
            if (File.Exists(DbPath)) File.Delete(DbPath);
            if (File.Exists(AttachDbPath)) File.Delete(AttachDbPath);

            // SQLite connection strings are as simple as "Data Source=...;Version=3;"
            string connStr = $"Data Source={DbPath};Version=3;";

            using (SQLiteConnection conn = new SQLiteConnection(connStr))
            {
                conn.Open();

                using (SQLiteCommand cmd = conn.CreateCommand())
                {
                    // ------------------------------------------------------
                    // SQLiteExpress s = new SQLiteExpress(cmd);
                    // ------------------------------------------------------
                    // Pass in the SQLiteCommand and use "s" as the gateway
                    // to every helper SQLiteExpress exposes.
                    // ------------------------------------------------------
                    SQLiteExpress s = new SQLiteExpress(cmd);

                    Demo01_CreateTables(s);
                    Demo02_RawExecuteAndSelect(s);
                    Demo03_Insert(s);
                    Demo04_ParameterizedQueries(s);
                    Demo05_ExecuteScalar(s);
                    Demo06_Update(s);
                    Demo07_InsertOrReplace(s);
                    Demo08_InsertUpdate_Upsert(s);
                    Demo09_SaveAndSaveList(s);
                    Demo10_GetObject_GetObjectList(s);
                    Demo11_Transactions(s);
                    Demo12_StringHelpers(s);
                    Demo13_DbInfo(s);
                    Demo14_TableOperations(s);
                    Demo15_AttachDetach(s);
                    Demo16_CodeGeneration(s);
                }
            }

            Console.WriteLine();
            Console.WriteLine("=== All demos completed. Press any key to exit. ===");
            Console.ReadKey();
        }

        // ==========================================================
        // Demo 01 — CreateTable using SQLiteTable / SQLiteColumn
        // ==========================================================
        static void Demo01_CreateTables(SQLiteExpress s)
        {
            WriteHeader("Demo 01 — CreateTable (SQLiteTable / SQLiteColumn)");

            // Build the "users" table using the fluent column model.
            SQLiteTable tUser = new SQLiteTable("users");
            tUser.Columns.Add(new SQLiteColumn("id", autoIncrement: true));        // integer primary key autoincrement
            tUser.Columns.Add(new SQLiteColumn("username", ColType.Text));
            tUser.Columns.Add(new SQLiteColumn("email", ColType.Text));
            tUser.Columns.Add(new SQLiteColumn("age", ColType.Integer));
            tUser.Columns.Add(new SQLiteColumn("balance", ColType.Decimal));
            tUser.Columns.Add(new SQLiteColumn(
                colName: "created_at",
                colDataType: ColType.DateTime,
                primaryKey: false,
                autoIncrement: false,
                notNull: true,
                defaultValue: "2026-01-01 00:00:00"));

            s.CreateTable(tUser);

            // "products" — composite primary key example (sku is PK, no autoincrement).
            SQLiteTable tProduct = new SQLiteTable("products");
            tProduct.Columns.Add(new SQLiteColumn(
                "sku", ColType.Text, primaryKey: true, autoIncrement: false, notNull: true, defaultValue: ""));
            tProduct.Columns.Add(new SQLiteColumn("name", ColType.Text));
            tProduct.Columns.Add(new SQLiteColumn("price", ColType.Decimal));
            tProduct.Columns.Add(new SQLiteColumn("stock", ColType.Integer));
            s.CreateTable(tProduct);

            // "logs" — a plain text log table for demos later.
            SQLiteTable tLog = new SQLiteTable("logs");
            tLog.Columns.Add(new SQLiteColumn("id", autoIncrement: true));
            tLog.Columns.Add(new SQLiteColumn("message", ColType.Text));
            tLog.Columns.Add(new SQLiteColumn("level", ColType.Text));
            s.CreateTable(tLog);

            Console.WriteLine("Created tables: users, products, logs");
        }

        // ==========================================================
        // Demo 02 — Execute (no-return) and Select (DataTable)
        // ==========================================================
        static void Demo02_RawExecuteAndSelect(SQLiteExpress s)
        {
            WriteHeader("Demo 02 — Raw Execute and Select");

            // Execute runs any statement that returns no rows.
            s.Execute("create index if not exists idx_users_email on users(email);");
            Console.WriteLine("Created index idx_users_email.");

            // Select returns a DataTable — good for ad-hoc queries.
            DataTable dt = s.Select("select name from sqlite_master where type='index';");
            Console.WriteLine($"Indexes found: {dt.Rows.Count}");
            foreach (DataRow dr in dt.Rows)
            {
                Console.WriteLine($"  - {dr["name"]}");
            }
        }

        // ==========================================================
        // Demo 03 — Insert via Dictionary<string, object>
        // ==========================================================
        static void Demo03_Insert(SQLiteExpress s)
        {
            WriteHeader("Demo 03 — Insert (Dictionary based)");

            Dictionary<string, object> dic = new Dictionary<string, object>();
            dic["username"] = "alice";
            dic["email"] = "alice@example.com";
            dic["age"] = 30;
            dic["balance"] = 1250.75m;
            dic["created_at"] = DateTime.Now;

            s.Insert("users", dic);

            // LastInsertId reads sqlite's last_insert_rowid().
            long newId = s.LastInsertId;
            Console.WriteLine($"Inserted alice, new id = {newId}");

            // Insert a few more so later demos have data to work with.
            s.Insert("users", new Dictionary<string, object>
            {
                { "username", "bob"   }, { "email", "bob@example.com"   },
                { "age", 42 }, { "balance", 300m   }, { "created_at", DateTime.Now },
            });
            s.Insert("users", new Dictionary<string, object>
            {
                { "username", "carol" }, { "email", "carol@example.com" },
                { "age", 27 }, { "balance", 9999.99m }, { "created_at", DateTime.Now },
            });
            s.Insert("users", new Dictionary<string, object>
            {
                { "username", "dave"  }, { "email", "dave@example.com"  },
                { "age", 55 }, { "balance", 50m    }, { "created_at", DateTime.Now },
            });

            Console.WriteLine("Inserted bob, carol, dave.");
        }

        // ==========================================================
        // Demo 04 — Parameterized Select (both dictionary and SQLiteParameter)
        // ==========================================================
        static void Demo04_ParameterizedQueries(SQLiteExpress s)
        {
            WriteHeader("Demo 04 — Parameterized Queries");

            // Style A: Dictionary<string, object> — shortest form.
            Dictionary<string, object> dicParam = new Dictionary<string, object>();
            dicParam["@minAge"] = 30;

            DataTable dt1 = s.Select(
                "select username, age from users where age >= @minAge order by age;",
                dicParam);

            Console.WriteLine("Users age >= 30 (dictionary params):");
            foreach (DataRow dr in dt1.Rows)
            {
                Console.WriteLine($"  {dr["username"]} ({dr["age"]})");
            }

            // Style B: IEnumerable<SQLiteParameter> — when you want explicit DbType.
            List<SQLiteParameter> plist = new List<SQLiteParameter>();
            plist.Add(new SQLiteParameter("@name", "bob"));

            DataTable dt2 = s.Select(
                "select username, email from users where username = @name;",
                plist);

            Console.WriteLine();
            Console.WriteLine("User 'bob' (SQLiteParameter list):");
            foreach (DataRow dr in dt2.Rows)
            {
                Console.WriteLine($"  {dr["username"]} -> {dr["email"]}");
            }
        }

        // ==========================================================
        // Demo 05 — ExecuteScalar (object and generic <T>)
        // ==========================================================
        static void Demo05_ExecuteScalar(SQLiteExpress s)
        {
            WriteHeader("Demo 05 — ExecuteScalar");

            // Plain ExecuteScalar returns object — you cast manually.
            object rawCount = s.ExecuteScalar("select count(*) from users;");
            Console.WriteLine($"Total users (object): {rawCount}");

            // Generic ExecuteScalar<T> converts for you.
            int userCount = s.ExecuteScalar<int>("select count(*) from users;");
            Console.WriteLine($"Total users (int):    {userCount}");

            decimal totalBalance = s.ExecuteScalar<decimal>("select sum(balance) from users;");
            Console.WriteLine($"Sum of balances:      {totalBalance}");

            // With parameters.
            string topSpender = s.ExecuteScalar<string>(
                "select username from users where balance = (select max(balance) from users);");
            Console.WriteLine($"Top spender:          {topSpender}");

            long aliceAge = s.ExecuteScalar<long>(
                "select age from users where username = @u;",
                new Dictionary<string, object> { { "@u", "alice" } });
            Console.WriteLine($"Alice's age (long):   {aliceAge}");
        }

        // ==========================================================
        // Demo 06 — Update (single col cond and multi-col cond)
        // ==========================================================
        static void Demo06_Update(SQLiteExpress s)
        {
            WriteHeader("Demo 06 — Update");

            // Update bob's balance — single condition column.
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["balance"] = 500m;
            data["age"] = 43;
            s.Update("users", data, "username", "bob");
            Console.WriteLine("Updated bob (single condition).");

            // Update with multiple conditions using a dictionary.
            Dictionary<string, object> data2 = new Dictionary<string, object>();
            data2["email"] = "carol.new@example.com";

            Dictionary<string, object> cond2 = new Dictionary<string, object>();
            cond2["username"] = "carol";
            cond2["age"] = 27;

            // updateSingleRow = false → no LIMIT 1 appended.
            s.Update("users", data2, cond2, updateSingleRow: false);
            Console.WriteLine("Updated carol (multi-column condition, no limit).");

            DataTable dt = s.Select("select username, email, balance, age from users order by id;");
            Console.WriteLine();
            Console.WriteLine("Users after updates:");
            PrintDataTable(dt);
        }

        // ==========================================================
        // Demo 07 — InsertOrReplace (upsert, whole row replacement)
        // ==========================================================
        static void Demo07_InsertOrReplace(SQLiteExpress s)
        {
            WriteHeader("Demo 07 — InsertOrReplace");

            // Products has a TEXT primary key, so we know the key up front.
            Dictionary<string, object> p = new Dictionary<string, object>();
            p["sku"] = "SKU-001";
            p["name"] = "USB Cable";
            p["price"] = 5.50m;
            p["stock"] = 100;
            s.InsertOrReplace("products", p);
            Console.WriteLine("InsertOrReplace SKU-001 (first time -> insert).");

            // Same SKU — all fields are replaced, not merged.
            Dictionary<string, object> p2 = new Dictionary<string, object>();
            p2["sku"] = "SKU-001";
            p2["name"] = "USB-C Cable (v2)";
            p2["price"] = 7.00m;
            p2["stock"] = 250;
            s.InsertOrReplace("products", p2);
            Console.WriteLine("InsertOrReplace SKU-001 (second time -> whole row replaced).");

            DataTable dt = s.Select("select * from products;");
            PrintDataTable(dt);
        }

        // ==========================================================
        // Demo 08 — InsertUpdate (ON CONFLICT DO UPDATE, partial upsert)
        // ==========================================================
        static void Demo08_InsertUpdate_Upsert(SQLiteExpress s)
        {
            WriteHeader("Demo 08 — InsertUpdate (upsert)");

            // First call: row does not exist → plain INSERT.
            Dictionary<string, object> p = new Dictionary<string, object>();
            p["sku"] = "SKU-002";
            p["name"] = "HDMI Cable";
            p["price"] = 12.00m;
            p["stock"] = 40;

            // On conflict, update ONLY price and stock (name stays as first inserted).
            List<string> updateCols = new List<string> { "price", "stock" };
            s.InsertUpdate("products", p, updateCols);
            Console.WriteLine("InsertUpdate SKU-002 (first call -> insert).");

            // Second call: same PK → updates price/stock but keeps original name.
            Dictionary<string, object> p2 = new Dictionary<string, object>();
            p2["sku"] = "SKU-002";
            p2["name"] = "This name will be IGNORED";   // not in update list
            p2["price"] = 15.00m;
            p2["stock"] = 60;
            s.InsertUpdate("products", p2, updateCols);
            Console.WriteLine("InsertUpdate SKU-002 (second call -> price/stock updated, name preserved).");

            // Include/exclude overload: update everything EXCEPT 'sku'.
            Dictionary<string, object> p3 = new Dictionary<string, object>();
            p3["sku"] = "SKU-002";
            p3["name"] = "Now the name changes too";
            p3["price"] = 15.50m;
            p3["stock"] = 55;
            s.InsertUpdate("products", p3, new[] { "sku" }, include: false);
            Console.WriteLine("InsertUpdate SKU-002 (exclude mode -> every non-PK column updated).");

            DataTable dt = s.Select("select * from products;");
            PrintDataTable(dt);
        }

        // ==========================================================
        // Demo 09 — Save / SaveList (reflection-based on class objects)
        // ==========================================================
        static void Demo09_SaveAndSaveList(SQLiteExpress s)
        {
            WriteHeader("Demo 09 — Save / SaveList");

            // Save a single object (note: Save uses InsertOrReplace internally).
            Product single = new Product
            {
                sku = "SKU-003",
                name = "Power Adapter",
                price = 25.00m,
                stock = 15
            };
            s.Save("products", single);
            Console.WriteLine("Save(single) -> SKU-003 inserted via reflection.");

            // SaveList for a batch.
            List<Product> batch = new List<Product>
            {
                new Product { sku = "SKU-004", name = "Mouse",    price =  9.99m, stock = 200 },
                new Product { sku = "SKU-005", name = "Keyboard", price = 29.99m, stock =  75 },
                new Product { sku = "SKU-006", name = "Monitor",  price = 199.00m, stock = 12 },
            };
            s.SaveList("products", batch);
            Console.WriteLine($"SaveList -> {batch.Count} products written in one go.");

            DataTable dt = s.Select("select * from products order by sku;");
            PrintDataTable(dt);
        }

        // ==========================================================
        // Demo 10 — GetObject<T> and GetObjectList<T>
        // ==========================================================
        static void Demo10_GetObject_GetObjectList(SQLiteExpress s)
        {
            WriteHeader("Demo 10 — GetObject / GetObjectList");

            // Single row → object. Columns map to fields or properties by name.
            User alice = s.GetObject<User>(
                "select * from users where username = @u;",
                new Dictionary<string, object> { { "@u", "alice" } });

            Console.WriteLine("GetObject<User> for alice:");
            Console.WriteLine($"  id={alice.id}, username={alice.username}, email={alice.email}");
            Console.WriteLine($"  age={alice.age}, balance={alice.balance}, created_at={alice.created_at}");

            // Fill an existing instance (useful for view-model hydration).
            User shell = new User();
            s.GetObject<User>("select * from users where username='bob';", shell);
            Console.WriteLine();
            Console.WriteLine($"GetObject<User>(existing shell) -> {shell.username}, balance={shell.balance}");

            // Whole list.
            List<User> all = s.GetObjectList<User>("select * from users order by id;");
            Console.WriteLine();
            Console.WriteLine($"GetObjectList<User> -> {all.Count} users:");
            foreach (User u in all)
            {
                Console.WriteLine($"  [{u.id}] {u.username,-8} {u.email,-25} age={u.age}, bal={u.balance}");
            }
        }

        // ==========================================================
        // Demo 11 — Transactions (commit + rollback)
        // ==========================================================
        static void Demo11_Transactions(SQLiteExpress s)
        {
            WriteHeader("Demo 11 — Transactions");

            int before = s.ExecuteScalar<int>("select count(*) from logs;");
            Console.WriteLine($"logs count before: {before}");

            // Commit path: insert two rows inside a transaction.
            s.BeginTransaction();
            try
            {
                s.Insert("logs", new Dictionary<string, object>
                {
                    { "message", "Startup complete" }, { "level", "INFO" },
                });
                s.Insert("logs", new Dictionary<string, object>
                {
                    { "message", "User alice logged in" }, { "level", "INFO" },
                });
                s.Commit();
                Console.WriteLine("Committed 2 log rows.");
            }
            catch
            {
                s.Rollback();
                throw;
            }

            // Rollback path: deliberately throw to test rollback.
            s.BeginTransaction();
            try
            {
                s.Insert("logs", new Dictionary<string, object>
                {
                    { "message", "This row must NOT persist" }, { "level", "WARN" },
                });

                // Simulate business-rule failure.
                throw new InvalidOperationException("Simulated failure -> rollback.");
            }
            catch (InvalidOperationException ex)
            {
                s.Rollback();
                Console.WriteLine($"Rolled back as expected: {ex.Message}");
            }

            int after = s.ExecuteScalar<int>("select count(*) from logs;");
            Console.WriteLine($"logs count after:  {after}  (should be before + 2)");
        }

        // ==========================================================
        // Demo 12 — String helpers: Escape, GetLikeString, GenerateContainsString
        // ==========================================================
        static void Demo12_StringHelpers(SQLiteExpress s)
        {
            WriteHeader("Demo 12 — String Helpers");

            // Escape() doubles single quotes so strings are safe to inline.
            string tricky = "O'Reilly's \"book\"";
            Console.WriteLine($"Escape: {tricky}  ->  {s.Escape(tricky)}");

            // GetLikeString wraps each whitespace-separated token with % for LIKE queries.
            string likePattern = s.GetLikeString("usb cable");
            Console.WriteLine($"GetLikeString(\"usb cable\") -> {likePattern}");

            // Give Carol a second row to match this LIKE pattern.
            s.Insert("users", new Dictionary<string, object>
            {
                { "username", "eve" }, { "email", "eve@fast.example.com" },
                { "age", 31 }, { "balance", 120m }, { "created_at", DateTime.Now },
            });

            // GenerateContainsString builds parameterized "col LIKE %a% AND col LIKE %b%".
            StringBuilder sb = new StringBuilder();
            sb.Append("select username, email from users where 1=1");

            Dictionary<string, object> dicParam = new Dictionary<string, object>();
            s.GenerateContainsString("email", "example com", sb, dicParam);
            sb.Append(" order by username;");

            Console.WriteLine();
            Console.WriteLine($"Generated SQL: {sb}");
            Console.WriteLine("Parameters:");
            foreach (var kv in dicParam)
                Console.WriteLine($"  {kv.Key} = {kv.Value}");

            DataTable dt = s.Select(sb.ToString(), dicParam);
            Console.WriteLine();
            Console.WriteLine($"Matches: {dt.Rows.Count}");
            foreach (DataRow dr in dt.Rows)
                Console.WriteLine($"  {dr["username"]} -> {dr["email"]}");
        }

        // ==========================================================
        // Demo 13 — DB info: tables, columns, database list
        // ==========================================================
        static void Demo13_DbInfo(SQLiteExpress s)
        {
            WriteHeader("Demo 13 — DB Info");

            List<string> tables = s.GetTableList();
            Console.WriteLine($"Tables ({tables.Count}):");
            foreach (string t in tables)
                Console.WriteLine($"  - {t}");

            Console.WriteLine();
            Console.WriteLine("Columns of 'users':");
            DataTable cols = s.GetColumnStatus("users");
            foreach (DataRow dr in cols.Rows)
            {
                Console.WriteLine(
                    $"  cid={dr["cid"]}, name={dr["name"]}, type={dr["type"]}, " +
                    $"notnull={dr["notnull"]}, dflt={dr["dflt_value"]}, pk={dr["pk"]}");
            }

            Console.WriteLine();
            Console.WriteLine("Database list (pragma database_list):");
            DataTable dbs = s.ShowDatabase();
            foreach (DataRow dr in dbs.Rows)
                Console.WriteLine($"  seq={dr["seq"]}, name={dr["name"]}, file={dr["file"]}");
        }

        // ==========================================================
        // Demo 14 — Table ops: rename, copy, drop, UpdateTableStructure
        // ==========================================================
        static void Demo14_TableOperations(SQLiteExpress s)
        {
            WriteHeader("Demo 14 — Table Operations");

            // Make a throwaway table to demonstrate rename/copy/drop.
            SQLiteTable tTemp = new SQLiteTable("temp_orig");
            tTemp.Columns.Add(new SQLiteColumn("id", autoIncrement: true));
            tTemp.Columns.Add(new SQLiteColumn("label", ColType.Text));
            s.CreateTable(tTemp);

            s.Insert("temp_orig", new Dictionary<string, object> { { "label", "one" } });
            s.Insert("temp_orig", new Dictionary<string, object> { { "label", "two" } });

            // RenameTable.
            s.RenameTable("temp_orig", "temp_renamed");
            Console.WriteLine("Renamed temp_orig -> temp_renamed");

            // Create a target table and CopyAllData between them.
            SQLiteTable tCopy = new SQLiteTable("temp_copy");
            tCopy.Columns.Add(new SQLiteColumn("id", autoIncrement: true));
            tCopy.Columns.Add(new SQLiteColumn("label", ColType.Text));
            s.CreateTable(tCopy);

            s.CopyAllData("temp_renamed", "temp_copy");
            int copied = s.ExecuteScalar<int>("select count(*) from temp_copy;");
            Console.WriteLine($"Copied all data to temp_copy -> {copied} row(s).");

            // UpdateTableStructure: rebuild temp_copy with an extra column while keeping the data.
            SQLiteTable newStruct = new SQLiteTable("temp_copy"); // TableName gets overwritten internally
            newStruct.Columns.Add(new SQLiteColumn("id", autoIncrement: true));
            newStruct.Columns.Add(new SQLiteColumn("label", ColType.Text));
            newStruct.Columns.Add(new SQLiteColumn("extra", ColType.Text));
            s.UpdateTableStructure("temp_copy", newStruct);
            Console.WriteLine("UpdateTableStructure temp_copy (added 'extra' column, data preserved).");

            // Confirm the new structure.
            DataTable dtInfo = s.GetColumnStatus("temp_copy");
            Console.Write("  columns now: ");
            foreach (DataRow dr in dtInfo.Rows) Console.Write(dr["name"] + " ");
            Console.WriteLine();

            // DropTable.
            s.DropTable("temp_renamed");
            s.DropTable("temp_copy");
            Console.WriteLine("Dropped temp_renamed, temp_copy.");
        }

        // ==========================================================
        // Demo 15 — AttachDatabase / DetachDatabase
        // ==========================================================
        static void Demo15_AttachDetach(SQLiteExpress s)
        {
            WriteHeader("Demo 15 — Attach / Detach Database");

            // Build a small secondary database file.
            string attachConnStr = $"Data Source={AttachDbPath};Version=3;";
            using (SQLiteConnection c2 = new SQLiteConnection(attachConnStr))
            {
                c2.Open();
                using (SQLiteCommand cmd2 = c2.CreateCommand())
                {
                    SQLiteExpress s2 = new SQLiteExpress(cmd2);
                    SQLiteTable t = new SQLiteTable("cities");
                    t.Columns.Add(new SQLiteColumn("id", autoIncrement: true));
                    t.Columns.Add(new SQLiteColumn("name", ColType.Text));
                    s2.CreateTable(t);
                    s2.Insert("cities", new Dictionary<string, object> { { "name", "Tawau"  } });
                    s2.Insert("cities", new Dictionary<string, object> { { "name", "Sandakan" } });
                    s2.Insert("cities", new Dictionary<string, object> { { "name", "Kota Kinabalu" } });
                }
            }

            // Attach it under alias "ext" and query across databases.
            s.AttachDatabase(AttachDbPath, "ext");
            DataTable dt = s.Select("select id, name from ext.cities order by id;");
            Console.WriteLine("Rows from ext.cities (attached):");
            foreach (DataRow dr in dt.Rows)
                Console.WriteLine($"  [{dr["id"]}] {dr["name"]}");

            s.DetachDatabase("ext");
            Console.WriteLine("Detached 'ext'.");
        }

        // ==========================================================
        // Demo 16 — Code generation helpers
        // ==========================================================
        static void Demo16_CodeGeneration(SQLiteExpress s)
        {
            WriteHeader("Demo 16 — Code Generation");

            // Three flavours of class-field generation.
            Console.WriteLine("--- PublicProperties ---");
            Console.WriteLine(s.GenerateTableClassFields("users", FieldsOutputType.PublicProperties));

            Console.WriteLine();
            Console.WriteLine("--- PublicFields ---");
            Console.WriteLine(s.GenerateTableClassFields("users", FieldsOutputType.PublicFields));

            Console.WriteLine();
            Console.WriteLine("--- PrivateFielsPublicProperties ---");
            Console.WriteLine(s.GenerateTableClassFields("users", FieldsOutputType.PrivateFielsPublicProperties));

            // Custom SQL (e.g. a join or projection) → class fields.
            Console.WriteLine();
            Console.WriteLine("--- Custom SQL (projection) ---");
            Console.WriteLine(s.GenerateCustomClassField(
                "select username, age, balance from users where 1=0;",
                FieldsOutputType.PublicProperties));

            // Helper snippets for parameter dictionaries and update column lists.
            Console.WriteLine();
            Console.WriteLine("--- Dictionary entry template for 'users' ---");
            Console.WriteLine(s.GenerateTableDictionaryEntries("users"));

            Console.WriteLine();
            Console.WriteLine("--- Parameter dictionary template for 'users' ---");
            Console.WriteLine(s.GenerateParameterDictionaryTable("users"));

            Console.WriteLine();
            Console.WriteLine("--- Non-PK update column list for 'users' ---");
            Console.WriteLine(s.GenerateUpdateColumnList("users"));

            Console.WriteLine();
            Console.WriteLine("--- CREATE TABLE SQL that SQLite stored for 'users' ---");
            Console.WriteLine(s.GetCreateTableSql("users"));
        }

        // ==========================================================
        // Small console utilities
        // ==========================================================
        static void WriteHeader(string title)
        {
            Console.WriteLine();
            Console.WriteLine(new string('=', 60));
            Console.WriteLine(title);
            Console.WriteLine(new string('=', 60));
        }

        static void PrintDataTable(DataTable dt)
        {
            // Header.
            foreach (DataColumn dc in dt.Columns)
                Console.Write(dc.ColumnName.PadRight(22));
            Console.WriteLine();
            Console.WriteLine(new string('-', dt.Columns.Count * 22));

            // Rows.
            foreach (DataRow dr in dt.Rows)
            {
                foreach (DataColumn dc in dt.Columns)
                {
                    string v = dr[dc] == DBNull.Value ? "(null)" : dr[dc].ToString();
                    if (v.Length > 20) v = v.Substring(0, 20);
                    Console.Write(v.PadRight(22));
                }
                Console.WriteLine();
            }
        }
    }

    // --------------------------------------------------------------
    // POCOs used by Save / SaveList / GetObject / GetObjectList.
    // Field/property names MUST match the table column names because
    // SQLiteExpress maps them reflectively.
    // --------------------------------------------------------------
    public class User
    {
        public long id;
        public string username;
        public string email;
        public int age;
        public decimal balance;
        public DateTime created_at;
    }

    public class Product
    {
        public string sku { get; set; }
        public string name { get; set; }
        public decimal price { get; set; }
        public int stock { get; set; }
    }
}
