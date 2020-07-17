using Azure;
using System;
using System.Collections.Generic;
using System.Text;

namespace SampleClientApp
{
    public static class Utilities
    {
        public static bool IgnoreRequestFailedException(Action operation)
        {
            if (operation == null)
                return false;

            try
            {
                operation.Invoke();
            }
            catch (RequestFailedException)
            {
                return false;
            }

            return true;
        }
    }
}
