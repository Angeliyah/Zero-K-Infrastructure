using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ZkData
{
    public class MiscVar
    {
        private static ConcurrentDictionary<string, string> cache = new ConcurrentDictionary<string, string>();
        public static string DefaultEngine { get { return GetValue("engine") ?? GlobalConst.DefaultEngineOverride; } set { SetValue("engine", value); } }


        [Key]
        [StringLength(200)]
        public string VarName { get; set; }
        public string VarValue { get; set; }

        public static string GetValue(string varName)
        {
            return cache.GetOrAdd(varName,
                (vn) =>
                {
                    using (var db = new ZkDataContext())
                    {
                        return db.MiscVars.Where(x => x.VarName == varName).Select(x => x.VarValue).FirstOrDefault();
                    }
                });
        }

        public static void SetValue(string varName, string value)
        {
            cache.AddOrUpdate(varName,
                (vn) =>
                {
                    StoreDbValue(varName, value);
                    return value;
                },
                (vn, val) =>
                {
                    if (val != value) StoreDbValue(varName, value);
                    return value;
                });
        }

        private static void StoreDbValue(string varName, string value)
        {
            using (var db = new ZkDataContext())
            {
                var entry = db.MiscVars.FirstOrDefault(x => x.VarName == varName);
                if (entry == null)
                {
                    entry = new MiscVar() { VarName = varName };
                    db.MiscVars.Add(entry);
                }
                entry.VarValue = value;
                db.SaveChanges();
            }
        }
    }
}