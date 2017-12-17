﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using ZkData;

namespace ZeroKWeb.Controllers
{
    public class ReplaysController : Controller
    {
        //
        // GET: /Replays/

        public ActionResult Index() {
            return Content("");
        }
        
    
        [OutputCache(Duration = int.MaxValue, VaryByParam = "none")]
        public ActionResult Download(string name) {
            if (string.IsNullOrEmpty(name)) {
                return Content("");
            }
            else {
                foreach (var p in GlobalConst.ReplaysPossiblePaths) {
                    var path = Path.Combine(p, name);
                    if (System.IO.File.Exists(path)) return File(System.IO.File.OpenRead(path), "application/octet-stream");
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.NotFound, "Demo file not found");
        }

    }
}
