using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IQMedia.Service.Domain;

namespace IQMedia.Service.Logic
{
    public class RootPathLogic : BaseLogic, ILogic
    {
        public RootPath GetRootPathByID(int p_ID)
        {
            return Context.GetRootPathLocationByID(p_ID);
        }
    }
}
