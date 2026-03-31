using System;
using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Category5.Core
{
    // handles unity relay service initialization, allocation, and join code flow
    public static class RelayHelper
    {
        // whether ugs has been initialized and signed in
        public static bool IsReady { get; private set; }
        
        // initialize unity gaming services and sign in anonymously
        // safe to call multiple times, skips if already done
        public static async Task InitializeAsync()
        {
            if (IsReady) return;
            
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                }
                
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                
                IsReady = true;
                // Debug.Log("RelayHelper: services initialized and signed in");
            }
            catch (Exception e)
            {
                Debug.LogError($"RelayHelper: failed to initialize services - {e.Message}");
                throw;
            }
        }
        
        // create a relay allocation and get a join code for others to use
        // maxConnections = number of OTHER players (so 3 for a 4-player game)
        public static async Task<(string joinCode, RelayServerData serverData)> CreateRelayAsync(int maxConnections)
        {
            if (!IsReady)
            {
                Debug.LogError("RelayHelper: services not initialized, call InitializeAsync first");
                throw new InvalidOperationException("Relay services not initialized");
            }
            
            // create the allocation on relay servers
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            
            // get a short join code for this allocation
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            // convert to transport-friendly data (dtls = encrypted udp)
            var serverData = allocation.ToRelayServerData("dtls");
            
            // Debug.Log($"RelayHelper: relay created, join code: {joinCode}");
            return (joinCode, serverData);
        }
        
        // join an existing relay allocation using a join code
        public static async Task<RelayServerData> JoinRelayAsync(string joinCode)
        {
            if (!IsReady)
            {
                Debug.LogError("RelayHelper: services not initialized, call InitializeAsync first");
                throw new InvalidOperationException("Relay services not initialized");
            }
            
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                throw new ArgumentException("Join code cannot be empty");
            }
            
            // join the allocation using the code
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim().ToUpperInvariant());
            
            // convert to transport-friendly data
            var serverData = joinAllocation.ToRelayServerData("dtls");
            
            // Debug.Log($"RelayHelper: joined relay with code: {joinCode}");
            return serverData;
        }
    }
}
