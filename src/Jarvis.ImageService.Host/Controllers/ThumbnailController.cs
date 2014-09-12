﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Jarvis.ImageService.Host.Controllers
{
    public class ThumbnailController : ApiController
    {
        [Route("thumbnail/upload")]
        [HttpPost]
        public string Upload()
        {
            return "Created @ " + DateTime.Now;
        }
    }
}
