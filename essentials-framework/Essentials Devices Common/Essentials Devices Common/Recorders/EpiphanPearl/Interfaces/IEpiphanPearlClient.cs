using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperDash.Core;

namespace PepperDash.Essentials.EpiphanPearl.Interfaces
{
    interface IEpiphanPearlClient
    {
        string Delete(string path);
        T Get<T>(string path) where T:class ;
        TResponse Post<TBody, TResponse>(string path, TBody body)
            where TBody : class
            where TResponse : class;

        TResponse Post<TResponse>(string path) where TResponse : class;
    }
}
