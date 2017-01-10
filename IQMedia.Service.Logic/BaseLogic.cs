using System;
using IQMedia.Service.Domain;
using System.Configuration;

namespace IQMedia.Service.Logic
{
    public abstract class BaseLogic
    {
        [ThreadStatic]
        private static IQMediaEntities _context;

        protected IQMediaEntities Context
        {
            get
            {
                if (_context != null)
                {
                    return _context;
                }
                else
                {
                    _context = new IQMediaEntities();
                    _context.CommandTimeout = Convert.ToInt32(ConfigurationManager.AppSettings["SqlCommandTimeout"]);
                    return _context;
                }
            }
        }

        /// <summary>
        /// Refreshes the context by disposing of it and setting it to null for the garbage
        /// collector.
        /// </summary>
        public static void RefreshContext()
        {
            if (_context != null)
            {
                _context.Dispose();
                _context = null;
            }
        }

        /// <summary>
        /// Persists all context changes to the data store.
        /// </summary>
        public void SaveChanges()
        {
            Context.SaveChanges();
        }
    }
}
