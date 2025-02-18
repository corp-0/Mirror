using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.RemoteCalls
{
    // invoke type for Cmd/Rpc
    public enum RemoteCallType { Command, ClientRpc }

    // remote call function delegate
    public delegate void RemoteCallDelegate(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection);

    /// UNITYSTATION CODE ///
    // Made public to allow access to custom fields.
    public class Invoker
    {
        // GameObjects might have multiple components of TypeA.CommandA().
        // when invoking, we check if 'TypeA' is an instance of the type.
        // the hash itself isn't enough because we wouldn't know which component
        // to invoke it on if there are multiple of the same type.
        public Type componentType;
        public RemoteCallType callType;
        public RemoteCallDelegate function;
        public bool cmdRequiresAuthority;

        public bool AreEqual(Type componentType, RemoteCallType remoteCallType, RemoteCallDelegate invokeFunction) =>
            this.componentType == componentType &&
            this.callType == remoteCallType &&
            this.function == invokeFunction;
    }

    /// <summary>Used to help manage remote calls for NetworkBehaviours</summary>
    public static class RemoteProcedureCalls
    {
        // one lookup for all remote calls.
        // allows us to easily add more remote call types without duplicating code.
        // note: do not clear those with [RuntimeInitializeOnLoad]
        static readonly Dictionary<int, Invoker> remoteCallDelegates = new Dictionary<int, Invoker>();

        static bool CheckIfDelegateExists(Type componentType, RemoteCallType remoteCallType, RemoteCallDelegate func, int functionHash)
        {
            if (remoteCallDelegates.ContainsKey(functionHash))
            {
                // something already registered this hash.
                // it's okay if it was the same function.
                Invoker oldInvoker = remoteCallDelegates[functionHash];
                if (oldInvoker.AreEqual(componentType, remoteCallType, func))
                {
                    return true;
                }

                // otherwise notify user. there is a rare chance of string
                // hash collisions.
                Debug.LogError($"Function {oldInvoker.componentType}.{oldInvoker.function.GetMethodName()} and {componentType}.{func.GetMethodName()} have the same hash.  Please rename one of them");
            }

            return false;
        }

        // pass full function name to avoid ClassA.Func & ClassB.Func collisions
        internal static int RegisterDelegate(Type componentType, string functionFullName, RemoteCallType remoteCallType, RemoteCallDelegate func, bool cmdRequiresAuthority = true)
        {
            // type+func so Inventory.RpcUse != Equipment.RpcUse
            int hash = functionFullName.GetStableHashCode();

            if (CheckIfDelegateExists(componentType, remoteCallType, func, hash))
                return hash;

            remoteCallDelegates[hash] = new Invoker
            {
                callType = remoteCallType,
                componentType = componentType,
                function = func,
                cmdRequiresAuthority = cmdRequiresAuthority
            };
            return hash;
        }

        // pass full function name to avoid ClassA.Func <-> ClassB.Func collisions
        // need to pass componentType to support invoking on GameObjects with
        // multiple components of same type with same remote call.
        public static void RegisterCommand(Type componentType, string functionFullName, RemoteCallDelegate func, bool requiresAuthority) =>
            RegisterDelegate(componentType, functionFullName, RemoteCallType.Command, func, requiresAuthority);

        // pass full function name to avoid ClassA.Func <-> ClassB.Func collisions
        // need to pass componentType to support invoking on GameObjects with
        // multiple components of same type with same remote call.
        public static void RegisterRpc(Type componentType, string functionFullName, RemoteCallDelegate func) =>
            RegisterDelegate(componentType, functionFullName, RemoteCallType.ClientRpc, func);

        // to clean up tests
        internal static void RemoveDelegate(int hash) =>
            remoteCallDelegates.Remove(hash);

        // note: no need to throw an error if not found.
        // an attacker might just try to call a cmd with an rpc's hash etc.
        // returning false is enough.
        static bool GetInvokerForHash(int functionHash, RemoteCallType remoteCallType, out Invoker invoker) =>
            remoteCallDelegates.TryGetValue(functionHash, out invoker) &&
            invoker != null &&
            invoker.callType == remoteCallType;

        // InvokeCmd/Rpc Delegate can all use the same function here
        internal static bool Invoke(int functionHash, RemoteCallType remoteCallType, NetworkReader reader, NetworkBehaviour component, NetworkConnectionToClient senderConnection = null)
        {
            // IMPORTANT: we check if the message's componentIndex component is
            //            actually of the right type. prevents attackers trying
            //            to invoke remote calls on wrong components.
            if (GetInvokerForHash(functionHash, remoteCallType, out Invoker invoker) &&
                invoker.componentType.IsInstanceOfType(component))
            {
                // invoke function on this component
                invoker.function(component, reader, senderConnection);
                mirrorProcessingCMD = false;

                return true;
            }
            return false;
        }

        // check if the command 'requiresAuthority' which is set in the attribute
        internal static bool CommandRequiresAuthority(int cmdHash) =>
            GetInvokerForHash(cmdHash, RemoteCallType.Command, out Invoker invoker) &&
            invoker.cmdRequiresAuthority;

        /// <summary>Gets the handler function by hash. Useful for profilers and debuggers.</summary>
        public static RemoteCallDelegate GetDelegate(int functionHash) =>
            remoteCallDelegates.TryGetValue(functionHash, out Invoker invoker)
            ? invoker.function
            : null;
    }
}

