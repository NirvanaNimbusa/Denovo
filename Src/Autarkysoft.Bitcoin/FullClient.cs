﻿// Autarkysoft.Bitcoin
// Copyright (c) 2020 Autarkysoft
// Distributed under the MIT software license, see the accompanying
// file LICENCE or http://www.opensource.org/licenses/mit-license.php.

using Autarkysoft.Bitcoin.Blockchain;
using Autarkysoft.Bitcoin.Cryptography;
using Autarkysoft.Bitcoin.P2PNetwork;
using Autarkysoft.Bitcoin.P2PNetwork.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Autarkysoft.Bitcoin
{
    /// <summary>
    /// Implementation of a full verifying node
    /// </summary>
    public class FullClient
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FullClient"/> with default properties.
        /// </summary>
        public FullClient()
        {
            AllNodes = new NodePool(Settings.MaxConnectionCount);
            Settings = new ClientSettings();
            Rng = new RandomNonceGenerator();
        }

        /// <summary>
        /// Initializes a new instance of <see cref="FullClient"/> using the given parameters.
        /// </summary>
        /// <param name="settings">Client settings</param>
        /// <param name="nodes">Node pool</param>
        public FullClient(IClientSettings settings, NodePool nodes)
        {
            Settings = settings;
            AllNodes = nodes;
            connector = new NodeConnector(AllNodes, settings);
            if (settings.AcceptIncomingConnections)
            {
                listener = new NodeListener(AllNodes, settings);
            }
        }


        private readonly NodeListener listener;
        private readonly NodeConnector connector;

        private const int DnsDigCount = 3;

        /// <summary>
        /// List of all nodes (peers) connected to this client.
        /// </summary>
        public NodePool AllNodes { get; set; }
        /// <summary>
        /// Settings used in this client
        /// </summary>
        public IClientSettings Settings { get; set; }
        /// <summary>
        /// Blockchain
        /// </summary>
        public IBlockchain Blockchain { get; set; }
        /// <summary>
        /// Storage instance
        /// </summary>
        public IStorage Storage { get; set; }
        /// <summary>
        /// A weak random number generator
        /// </summary>
        public IRandomNonceGenerator Rng { get; set; }


        private async Task<IPAddress[]> DigDnsSeeds(bool all)
        {
            List<IPAddress> result = new List<IPAddress>();
            int[] indices;
            if (all)
            {
                indices = Enumerable.Range(0, Settings.DnsSeeds.Length).ToArray();
            }
            else
            {
                indices = Rng.GetDistinct(0, Settings.DnsSeeds.Length, Math.Min(DnsDigCount, Settings.DnsSeeds.Length));
            }

            foreach (int i in indices)
            {
                try
                {
                    IPAddress[] temp = await Dns.GetHostAddressesAsync(Settings.DnsSeeds[i]);
                    if (!(temp is null))
                    {
                        result.AddRange(temp);
                    }
                }
                catch (Exception) { }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Start this client by selecting a random peer's IP address and connecting to it.
        /// </summary>
        public async void Start()
        {
            // When client starts, whether the blockchain is not synced at all (0 blocks) or partly synced (some blocks) or
            // fully synced (all blocks to the actual tip), the connection always starts with 1 other node to download
            // a "map" (ie. the block headers) to figure out what the local blockchain status actually is (behind, same
            // or ahead).
            // The message/reply mangers have to handle the sync process and raise an event to add more peers to the pool.
            Settings.IsCatchingUp = true;
            bool supportsIpV6 = Socket.OSSupportsIPv6;
            NetworkAddressWithTime[] addrs = Storage.ReadAddrs();
            if (addrs is null)
            {
                // Dig a small number of DNS seeds that are randomly chosen.
                IPAddress[] ips = await DigDnsSeeds(false);
                if (ips is null)
                {
                    // If random dig failed, dig all of them.
                    ips = await DigDnsSeeds(true);
                    if (ips is null)
                    {
                        // TODO: we need some sort of message manager or logger to post results, errors,... to
                        return;
                    }
                }

                int index = Rng.NextInt32() % ips.Length;
                await Task.Run(() => connector.StartConnect(new IPEndPoint(ips[index], Settings.Port)));
            }
            else
            {
                int max = Math.Min(Settings.MaxConnectionCount, addrs.Length);
                var indexes = Rng.GetDistinct(0, addrs.Length, max);
                await Task.Run(() =>
                {
                    for (int i = 0; i < max; i++)
                    {
                        var addr = addrs[indexes[i]];
                        if (!supportsIpV6 && addr.NodeIP.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            continue;
                        }
                        connector.StartConnect(new IPEndPoint(addr.NodeIP, addr.NodePort));
                    }
                });
            }

            if (!(listener is null))
            {
                listener.StartListen(new IPEndPoint(IPAddress.Any, Settings.Port));
            }
        }
    }
}
