﻿// Autarkysoft Tests
// Copyright (c) 2020 Autarkysoft
// Distributed under the MIT software license, see the accompanying
// file LICENCE or http://www.opensource.org/licenses/mit-license.php.

using Autarkysoft.Bitcoin;
using Autarkysoft.Bitcoin.Blockchain.Blocks;
using Autarkysoft.Bitcoin.Blockchain.Transactions;
using Autarkysoft.Bitcoin.P2PNetwork;
using Autarkysoft.Bitcoin.P2PNetwork.Messages;
using Autarkysoft.Bitcoin.P2PNetwork.Messages.MessagePayloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Tests.Bitcoin.Blockchain;
using Tests.Bitcoin.Blockchain.Blocks;
using Xunit;

namespace Tests.Bitcoin.P2PNetwork
{
    public class ReplyManagerTests
    {
        private const long RngReturnValue = 0x0158a8e8ba5f3ed3;


        [Fact]
        public void GetPingMsgTest()
        {
            var ns = new MockNodeStatus()
            {
                expPingNonce = RngReturnValue,
                storePingReturn = true
            };
            var cs = new MockClientSettings()
            {
                _netType = NetworkType.TestNet
            };

            var rep = new ReplyManager(ns, cs)
            {
                rng = new MockNonceRng(RngReturnValue)
            };


            Message msg = rep.GetPingMsg();
            FastStream actual = new FastStream();
            msg.Serialize(actual);
            byte[] expected = Helper.HexToBytes("0b11090770696e670000000000000000080000002a45a5d2d33e5fbae8a85801");

            Assert.Equal(expected, actual.ToByteArray());
        }

        [Fact]
        public void GetVersionMsgTest()
        {
            var ns = new MockNodeStatus()
            {
                _ip = IPAddress.Parse("1.2.3.4"),
                _portToReturn = 444
            };
            var cs = new MockClientSettings()
            {
                _protoVer = 123,
                _services = NodeServiceFlags.NodeNetwork | NodeServiceFlags.NodeWitness,
                _time = new MockClientTime() { _now = 456 },
                _ua = "foo",
                _relay = true,
                _netType = NetworkType.TestNet,
                _bchain = new MockBlockchain() { _height = 12345 }
            };

            var rep = new ReplyManager(ns, cs)
            {
                rng = new MockNonceRng(RngReturnValue)
            };


            Message msg = rep.GetVersionMsg();
            FastStream actual = new FastStream();
            msg.Serialize(actual);
            byte[] expected = Helper.HexToBytes("0b11090776657273696f6e0000000000590000002795abaa7b0000000900000000000000c801000000000000000000000000000000000000000000000000ffff0102030401bc0900000000000000000000000000000000000000000000000000d33e5fbae8a8580103666f6f3930000001");

            Assert.Equal(expected, actual.ToByteArray());
        }

        public static IEnumerable<object[]> GetReplyCases()
        {
            var bc = new MockBlockchain();
            var cs = new MockClientSettings() { _netType = NetworkType.MainNet, _bchain = bc };
            var mockAddr0 = new NetworkAddressWithTime(NodeServiceFlags.All, IPAddress.Loopback, 1010, 5678);
            var mockAddr1 = new NetworkAddressWithTime(NodeServiceFlags.All, IPAddress.Parse("200.2.3.4"), 23, 98);
            var mockAddr2 = new NetworkAddressWithTime(NodeServiceFlags.All, IPAddress.Parse("1.2.3.4"), 8080, 665412);
            var mockAddr3 = new NetworkAddressWithTime(NodeServiceFlags.All, IPAddress.Parse("99.77.88.66"), 444, 120000);

            var mockAddrs = new NetworkAddressWithTime[] { mockAddr1, mockAddr2, mockAddr3 };

            var mockAddrs1000 = Enumerable.Repeat(mockAddr0, 997).ToList();
            mockAddrs1000.AddRange(mockAddrs); // last 3 items are distict

            var mockAddrs1002 = Enumerable.Repeat(mockAddr0, 999).ToList();
            mockAddrs1002.AddRange(mockAddrs); // last 3 items are distict

            var expAddr1002_1 = Enumerable.Repeat(mockAddr0, 999).ToList();
            expAddr1002_1.Add(mockAddr1);
            var expAddr1002_2 = new NetworkAddressWithTime[2] { mockAddr2, mockAddr3 };

            var mockBlock = new MockSerializableBlock(Helper.HexToBytes("01000000161126f0d39ec082e51bbd29a1dfb40b416b445ac8e493f88ce993860000000030e2a3e32abf1663a854efbef1b233c67c8cdcef5656fe3b4f28e52112469e9bae306849ffff001d16d1b42d0101000000010000000000000000000000000000000000000000000000000000000000000000ffffffff0704ffff001d0116ffffffff0100f2052a01000000434104f5efde0c2d30ab28e3dbe804c1a4aaf13066f9b198a4159c76f8f79b3b20caf99f7c979ed6c71481061277a6fc8666977c249da99960c97c8d8714fda9f0e883ac00000000"));

            var tx = new Transaction();
            tx.TryDeserialize(new FastStreamReader(Helper.HexToBytes("01000000027993f8d801d045e9a7c92da4d231b86203b84930a84b7c97f5acf5fd5f44e27d0000000000ffffffff7993f8d801d045e9a7c92da4d231b86203b84930a84b7c97f5acf5fd5f44e27d0100000000ffffffff02a0860100000000001976a914b50a33e43bec2c74112d1d6ea42a174f435aabd688ac6e860100000000001976a914de00391342cfb6dd4a844d6cacec2fefe43cadf388ac37661900")), out _);

            yield return new object[]
            {
                // Addr
                new MockNodeStatus() { _handShakeToReturn = HandShakeState.Finished, updateTime = true },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    addrsToReceive = mockAddrs,
                },
                new Message(new AddrPayload(mockAddrs), NetworkType.MainNet),
                null
            };
            yield return new object[]
            {
                // Block
                new MockNodeStatus() { _handShakeToReturn = HandShakeState.Finished, updateTime = true },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _bchain = new MockBlockchain()
                    {
                        expProcessBlk = "00000000841cb802ca97cf20fb9470480cae9e5daa5d06b4a18ae2d5dd7f186f",
                        blkProcessSuccess = true
                    }
                },
                new Message(new BlockPayload(mockBlock), NetworkType.MainNet),
                null
            };
            yield return new object[]
            {
                // Block
                new MockNodeStatus() { _handShakeToReturn = HandShakeState.Finished, updateTime = true, mediumViolation = true },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _bchain = new MockBlockchain()
                    {
                        expProcessBlk = "00000000841cb802ca97cf20fb9470480cae9e5daa5d06b4a18ae2d5dd7f186f",
                        blkProcessSuccess = false
                    }
                },
                new Message(new BlockPayload(mockBlock), NetworkType.MainNet),
                null
            };
            yield return new object[]
            {
                // FeeFilter
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.Finished, updateTime = true, _relayToReturn = true, _fee = 12345
                },
                cs,
                new Message(new FeeFilterPayload(12345), NetworkType.MainNet),
                null
            };
            yield return new object[]
            {
                // FeeFilter (node doesn't relay)
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.Finished, updateTime = true, _relayToReturn = false, smallViolation = true
                },
                cs,
                new Message(new FeeFilterPayload(12345), NetworkType.MainNet),
                null
            };
            yield return new object[]
            {
                // FeeFilter (node wants to set an unreasonably huge fee filter)
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.Finished, updateTime = true,
                    _relayToReturn = true,
                    _relayToSet = false, // Relay is changed to false by ReplyManager
                    mediumViolation = true
                },
                cs,
                new Message(new FeeFilterPayload(444000_000UL), NetworkType.MainNet),
                null
            };
            yield return new object[]
            {
                // GetAddr with smaller than max items
                new MockNodeStatus() { _handShakeToReturn = HandShakeState.Finished, updateTime = true, _addrSent = false },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    addrsToReturn = mockAddrs,
                },
                new Message(new GetAddrPayload(), NetworkType.MainNet),
                new Message[1] { new Message(new AddrPayload(mockAddrs), NetworkType.MainNet) }
            };
            yield return new object[]
            {
                // GetAddr with max items
                new MockNodeStatus() { _handShakeToReturn = HandShakeState.Finished, updateTime = true, _addrSent = false },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    addrsToReturn = mockAddrs1000.ToArray(),
                },
                new Message(new GetAddrPayload(), NetworkType.MainNet),
                new Message[1] { new Message(new AddrPayload(mockAddrs1000.ToArray()), NetworkType.MainNet) }
            };
            yield return new object[]
            {
                // GetAddr with more than max items (needs 2 messages)
                new MockNodeStatus() { _handShakeToReturn = HandShakeState.Finished, updateTime = true, _addrSent = false },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    addrsToReturn = mockAddrs1002.ToArray(),
                },
                new Message(new GetAddrPayload(), NetworkType.MainNet),
                new Message[2]
                {
                    new Message(new AddrPayload(expAddr1002_1.ToArray()), NetworkType.MainNet),
                    new Message(new AddrPayload(expAddr1002_2), NetworkType.MainNet)
                }
            };
            yield return new object[]
            {
                // GetAddr with 0 items (no reply message)
                new MockNodeStatus() { _handShakeToReturn = HandShakeState.Finished, updateTime = true, _addrSent = false },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    addrsToReturn = new NetworkAddressWithTime[0],
                },
                new Message(new GetAddrPayload(), NetworkType.MainNet),
                null
            };
            yield return new object[]
            {
                // GetAddr but addr was already sent (no reply message)
                new MockNodeStatus() { _handShakeToReturn = HandShakeState.Finished, updateTime = true, _addrSent = true },
                new MockClientSettings() { _netType = NetworkType.MainNet },
                new Message(new GetAddrPayload(), NetworkType.MainNet),
                null
            };
            yield return new object[]
            {
                // Inv
                new MockNodeStatus() { _handShakeToReturn = HandShakeState.Finished, updateTime = true },
                new MockClientSettings() { _relay = true, _netType = NetworkType.MainNet },
                new Message(new InvPayload(new Inventory[] { new Inventory(InventoryType.Tx, new byte[32]) }), NetworkType.MainNet),
                null
            };
            yield return new object[]
            {
                // Inv (no relay + tx type inv is a violation)
                new MockNodeStatus() { _handShakeToReturn = HandShakeState.Finished, updateTime = true, bigViolation = true },
                new MockClientSettings() { _relay = false, _netType = NetworkType.MainNet },
                new Message(new InvPayload(new Inventory[] { new Inventory(InventoryType.Tx, new byte[32]) }), NetworkType.MainNet),
                null
            };
            yield return new object[]
            {
                // Ping
                new MockNodeStatus() { _handShakeToReturn = HandShakeState.Finished, updateTime = true },
                cs,
                new Message(new PingPayload(98765), NetworkType.MainNet),
                new Message[1] { new Message(new PongPayload(98765), NetworkType.MainNet) }
            };
            yield return new object[]
            {
                // Pong
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.Finished, updateTime = true, expPongNonce = 98765
                },
                cs,
                new Message(new PongPayload(98765), NetworkType.MainNet),
                null
            };
            yield return new object[]
            {
                // SendCmpct
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.Finished, updateTime = true, _sendCmpt = true, _CmptVer = 1
                },
                cs,
                new Message(new SendCmpctPayload(true, 1), NetworkType.MainNet),
                null
            };
            yield return new object[]
            {
                // SendCmpct
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.Finished, updateTime = true, _sendCmpt = false, _CmptVer = 2
                },
                cs,
                new Message(new SendCmpctPayload(false, 2), NetworkType.MainNet),
                null
            };
            yield return new object[]
            {
                // SendHeaders
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.Finished, updateTime = true, _sendHdrToReturn = false, _sendHdrToSet = true
                },
                cs,
                new Message(new SendHeadersPayload(), NetworkType.MainNet),
                null
            };
            yield return new object[]
            {
                // SendHeaders (it is a violation to send it again)
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.Finished, updateTime = true, _sendHdrToReturn = true, smallViolation = true
                },
                cs,
                new Message(new SendHeadersPayload(), NetworkType.MainNet),
                null
            };
            yield return new object[]
            {
                // Tx
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.Finished, updateTime = true,
                },
                new MockClientSettings()
                {
                    _relay = true,
                    _expMemPoolTx = tx,
                    _addToMemPoolReturn = true
                },
                new Message(new TxPayload(tx), NetworkType.MainNet),
                null
            };
            yield return new object[]
            {
                // Tx
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.Finished, updateTime = true, bigViolation = true
                },
                new MockClientSettings()
                {
                    _relay = false,
                },
                new Message(new TxPayload(tx), NetworkType.MainNet),
                null
            };
        }
        [Theory]
        [MemberData(nameof(GetReplyCases))]
        public void GetReplyTest(MockNodeStatus ns, IClientSettings cs, Message msg, Message[] expected)
        {
            var rep = new ReplyManager(ns, cs);

            Message[] actual = rep.GetReply(msg);

            if (expected is null)
            {
                Assert.Null(actual);
            }
            else
            {
                Assert.NotNull(actual);
                Assert.Equal(expected.Length, actual.Length);
                for (int i = 0; i < expected.Length; i++)
                {
                    var actualStream = new FastStream(Constants.MessageHeaderSize + actual[i].PayloadData.Length);
                    var expectedStream = new FastStream(Constants.MessageHeaderSize + expected[i].PayloadData.Length);
                    actual[i].Serialize(actualStream);
                    expected[i].Serialize(expectedStream);

                    Assert.Equal(expectedStream.ToByteArray(), actualStream.ToByteArray());
                }
            }

            // Mock will change the following bools to false if it were called.
            Assert.False(ns.updateTime, "UpdateTime() was never called");
            Assert.False(ns.smallViolation, "AddSmallViolation() was never called");
            Assert.False(ns.mediumViolation, "AddMediumViolation() was never called");
            Assert.False(ns.bigViolation, "AddBigViolation() was never called");
        }

        public static IEnumerable<object[]> GetDeserFailCases()
        {
            foreach (PayloadType item in Enum.GetValues(typeof(PayloadType)))
            {
                if (item != PayloadType.Alert && item != PayloadType.Reject && // Ignored messages
                    item != PayloadType.Verack && item != PayloadType.Version && // have separate tests
                    item != PayloadType.FilterClear && item != PayloadType.GetAddr &&
                    item != PayloadType.MemPool && item != PayloadType.SendHeaders && // Empty payload

                    // TODO: remove these after implementation
                    !new PayloadType[]
                    {
                        PayloadType.AddrV2, PayloadType.CFCheckpt, PayloadType.CFHeaders, PayloadType.CFilter,
                        PayloadType.GetCFCheckpt, PayloadType.GetCFHeaders, PayloadType.GetCFilters, PayloadType.SendAddrV2,
                        PayloadType.WTxIdRelay
                    }.Contains(item))
                {
                    yield return new object[] { item };
                }
            }
        }
        [Theory]
        [MemberData(nameof(GetDeserFailCases))]
        public void GetReply_FailToDeserializeTest(PayloadType plt)
        {
            var msg = new Message(new MockSerializableMessagePayload(plt, new byte[] { 255, 255, 255 }), NetworkType.MainNet);
            var ns = new MockNodeStatus()
            {
                _handShakeToReturn = HandShakeState.Finished,
                smallViolation = true,
                updateTime = true
            };
            var rep = new ReplyManager(ns, new ClientSettings());

            Message[] actual = rep.GetReply(msg);
            Assert.Null(actual);

            Assert.False(ns.updateTime);
            Assert.False(ns.smallViolation);
        }

        [Fact]
        public void GetReply_UndefinedPayloadTest()
        {
            var pl = new MockSerializableMessagePayload((PayloadType)10000, new byte[] { 1, 2, 3 });
            var msg = new Message(pl, NetworkType.MainNet);
            var ns = new MockNodeStatus()
            {
                _handShakeToReturn = HandShakeState.Finished,
                smallViolation = true,
                updateTime = true
            };
            var rep = new ReplyManager(ns, new MockClientSettings());

            var actual = rep.GetReply(msg);

            Assert.Null(actual);
            Assert.False(ns.updateTime);
        }

        [Fact]
        public void GetReply_NoHandShakeTest()
        {
            var msg = new Message(new GetAddrPayload(), NetworkType.MainNet);
            var ns = new MockNodeStatus()
            {
                _handShakeToReturn = HandShakeState.None,
                mediumViolation = true,
                updateTime = true
            };
            var rep = new ReplyManager(ns, new MockClientSettings());

            var actual = rep.GetReply(msg);

            Assert.Null(actual);
            Assert.False(ns.updateTime);
        }

        public static IEnumerable<object[]> GetIgnoredCases()
        {
            yield return new object[]
            {
                new Message(new MockSerializableMessagePayload(PayloadType.Alert, new byte[1]), NetworkType.MainNet)
            };
            yield return new object[]
            {
                new Message(new MockSerializableMessagePayload(PayloadType.Reject, new byte[1]), NetworkType.MainNet)
            };
        }
        [Theory]
        [MemberData(nameof(GetIgnoredCases))]
        public void GetReply_IgnoredMessagesTest(Message msg)
        {
            var ns = new MockNodeStatus()
            {
                _handShakeToReturn = HandShakeState.Finished,
                updateTime = true
            };
            var rep = new ReplyManager(ns, new MockClientSettings());

            var actual = rep.GetReply(msg);

            Assert.Null(actual);
            Assert.False(ns.updateTime);
        }

        public static IEnumerable<object[]> GetVerackCases()
        {
            var cs = new MockClientSettings();
            int mockProtoVer = 674;
            ulong feeRateSat = 123456;
            ulong feeRateKiloSat = 123456_000;
            var feeFilter = new Message(new FeeFilterPayload(feeRateKiloSat), NetworkType.MainNet);
            var ping = new Message(new PingPayload(RngReturnValue), NetworkType.MainNet);
            var sendHdr = new Message(new SendHeadersPayload(), NetworkType.MainNet);
            BlockHeader hdr = BlockHeaderTests.GetSampleBlockHeader();
            var getHdrs = new Message(new GetHeadersPayload(mockProtoVer, new BlockHeader[] { hdr }, null), NetworkType.MainNet);


            yield return new object[]
            {
                new MockNodeStatus() { _handShakeToReturn = HandShakeState.None, mediumViolation = true, updateTime = true },
                cs,
                null
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.Sent,
                    _handShakeToSet = HandShakeState.SentAndConfirmed,
                    updateTime = true
                },
                cs,
                null
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.SentAndConfirmed, mediumViolation = true, updateTime = true
                },
                cs,
                null
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.Finished, mediumViolation = true, updateTime = true
                },
                cs,
                null
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    _protVer = Constants.P2PMinProtoVer, // No ping, no feefilter, no sendheaders
                },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _catchup = false, // No sync (getheaders)
                    _relay = false, // No feefilter, no addr
                },
                null
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    _protVer = Constants.P2PMinProtoVer, // No ping, no feefilter, no sendheaders
                },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _catchup = false, // No sync (getheaders)
                    _relay = true, // No feefilter becaue of protocol version, addr
                    myIpToReturn = IPAddress.Loopback // No addr
                },
                null
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    _protVer = Constants.P2PBip31ProtVer, // No ping, no feefilter, no sendheaders
                },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _catchup = false, // No sync (getheaders)
                    _relay = false, // No feefilter, no addr
                },
                null
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    _protVer = Constants.P2PBip31ProtVer + 1, // ping, no feefilter, no sendheaders
                    expPingNonce = RngReturnValue
                },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _catchup = false, // No sync (getheaders)
                    _relay = true, // No feefilter becaue of protocol version, addr
                    myIpToReturn = IPAddress.Loopback // No addr
                },
                new Message[] { ping }
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    _protVer = Constants.P2PBip130ProtVer - 1, // ping, no feefilter, no sendheaders
                    expPingNonce = RngReturnValue
                },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _catchup = false, // No sync (getheaders)
                    _relay = true, // No feefilter becaue of protocol version, addr
                    myIpToReturn = IPAddress.Loopback // No addr
                },
                new Message[] { ping }
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    _protVer = Constants.P2PBip130ProtVer, // ping, no feefilter, sendheaders
                    expPingNonce = RngReturnValue
                },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _catchup = false, // No sync (getheaders)
                    _relay = true, // No feefilter becaue of protocol version, addr
                    myIpToReturn = IPAddress.Loopback // No addr
                },
                new Message[] { ping, sendHdr }
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    _protVer = Constants.P2PBip133ProtVer - 1, // ping, no feefilter, sendheaders
                    expPingNonce = RngReturnValue
                },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _catchup = false, // No sync (getheaders)
                    _relay = true, // No feefilter becaue of protocol version, addr
                    myIpToReturn = IPAddress.Loopback // No addr
                },
                new Message[] { ping, sendHdr }
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    _protVer = Constants.P2PBip133ProtVer, // ping, feefilter, sendheaders
                    expPingNonce = RngReturnValue
                },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _catchup = false, // No sync (getheaders)
                    _relay = false, // No feefilter, no addr
                },
                new Message[] { ping, sendHdr }
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    _protVer = Constants.P2PBip133ProtVer, // ping, feefilter, sendheaders
                    expPingNonce = RngReturnValue
                },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _catchup = false, // No sync (getheaders)
                    _relay = true, // feefilter, addr
                    myIpToReturn = IPAddress.Loopback, // No addr
                    _fee = feeRateSat
                },
                new Message[] { ping, sendHdr, feeFilter }
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    _protVer = Constants.P2PMinProtoVer, // No ping, No feefilter, No sendheaders
                    _servs = NodeServiceFlags.NodeNone | NodeServiceFlags.NodeGetUtxo |
                             NodeServiceFlags.NodeBloom | NodeServiceFlags.NodeWitness |
                             NodeServiceFlags.NodeXThin | NodeServiceFlags.NodeCompactFilters, // All except Network and Limited
                    expectDiscSignal = true
                },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _catchup = true, // Start sync (getheaders)
                    _relay = false, // No feefilter, no addr
                },
                null
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    _protVer = Constants.P2PMinProtoVer, // No ping, No feefilter, No sendheaders
                    _servs = NodeServiceFlags.NodeNetwork
                },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _catchup = true, // Start sync (getheaders)
                    _relay = false, // No feefilter, no addr
                    _protoVer = mockProtoVer,
                    _bchain = new MockBlockchain()
                    {
                        headerLocatorToReturn = new BlockHeader[] { hdr }
                    },
                },
                new Message[] { getHdrs }
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    _protVer = Constants.P2PBip31ProtVer, // No ping, No feefilter, No sendheaders
                    _servs = NodeServiceFlags.NodeNetworkLimited
                },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _catchup = true, // Start sync (getheaders)
                    _relay = false, // No feefilter, no addr
                    _protoVer = mockProtoVer,
                    _bchain = new MockBlockchain()
                    {
                        headerLocatorToReturn = new BlockHeader[] { hdr }
                    },
                },
                new Message[] { getHdrs }
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    _protVer = Constants.P2PBip31ProtVer + 1, // ping, No feefilter, No sendheaders
                    _servs = NodeServiceFlags.NodeNetwork | NodeServiceFlags.NodeNetworkLimited,
                    expPingNonce = RngReturnValue,
                },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _catchup = true, // Start sync (getheaders)
                    _relay = false, // No feefilter, no addr
                    _protoVer = mockProtoVer,
                    _bchain = new MockBlockchain()
                    {
                        headerLocatorToReturn = new BlockHeader[] { hdr }
                    },
                },
                new Message[] { ping, getHdrs }
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    _protVer = Constants.P2PBip130ProtVer, // ping, No feefilter, sendheaders
                    _servs = NodeServiceFlags.NodeNetwork | NodeServiceFlags.NodeNetworkLimited,
                    expPingNonce = RngReturnValue,
                },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _catchup = true, // Start sync (getheaders) => no sendheaders
                    _relay = false, // No feefilter, no addr
                    _protoVer = mockProtoVer,
                    _bchain = new MockBlockchain()
                    {
                        headerLocatorToReturn = new BlockHeader[] { hdr }
                    },
                },
                new Message[] { ping, getHdrs }
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    _protVer = Constants.P2PBip133ProtVer, // ping, feefilter, sendheaders
                    _servs = NodeServiceFlags.NodeNetwork | NodeServiceFlags.NodeNetworkLimited,
                    expPingNonce = RngReturnValue,
                },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _catchup = true, // Start sync (getheaders) => no sendheaders
                    _relay = false, // No feefilter, no addr
                    _protoVer = mockProtoVer,
                    _bchain = new MockBlockchain()
                    {
                        headerLocatorToReturn = new BlockHeader[] { hdr }
                    },
                },
                new Message[] { ping, getHdrs }
            };
            yield return new object[]
            {
                new MockNodeStatus()
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    _protVer = Constants.P2PBip133ProtVer, // ping, feefilter, sendheaders
                    _servs = NodeServiceFlags.NodeNetwork | NodeServiceFlags.NodeNetworkLimited,
                    expPingNonce = RngReturnValue,
                },
                new MockClientSettings()
                {
                    _netType = NetworkType.MainNet,
                    _catchup = true, // Start sync (getheaders) => no sendheaders, no feefilter
                    _relay = true, // feefilter, addr
                    myIpToReturn = IPAddress.Loopback, // No addr
                    _protoVer = mockProtoVer,
                    _bchain = new MockBlockchain()
                    {
                        headerLocatorToReturn = new BlockHeader[] { hdr }
                    },
                },
                new Message[] { ping, getHdrs }
            };
        }
        [Theory]
        [MemberData(nameof(GetVerackCases))]
        public void CheckVerackTest(MockNodeStatus ns, IClientSettings cs, Message[] expected)
        {
            var rep = new ReplyManager(ns, cs)
            {
                rng = new MockNonceRng(RngReturnValue)
            };
            var msg = new Message(new VerackPayload(), NetworkType.MainNet);

            Message[] actual = rep.GetReply(msg);
            if (expected is null)
            {
                Assert.Null(actual);
            }
            else
            {
                Assert.NotNull(actual);
                Assert.Equal(expected.Length, actual.Length);
                for (int i = 0; i < expected.Length; i++)
                {
                    var actualStream = new FastStream(Constants.MessageHeaderSize + actual[i].PayloadData.Length);
                    var expectedStream = new FastStream(Constants.MessageHeaderSize + expected[i].PayloadData.Length);
                    actual[i].Serialize(actualStream);
                    expected[i].Serialize(expectedStream);

                    Assert.Equal(expectedStream.ToByteArray(), actualStream.ToByteArray());
                }
            }

            // Mock will change the following bool to false if it were called.
            Assert.False(ns.updateTime, "UpdateTime() was never called");

            // Mock either doesn't have any h.s. to set or if it did set h.s. it was checked and then turned to null
            Assert.Null(ns._handShakeToSet);
        }

        public static IEnumerable<object[]> GetAddrCases()
        {
            yield return new object[] { IPAddress.Loopback, null };
            yield return new object[] { IPAddress.IPv6Loopback, null };
            yield return new object[]
            {
                IPAddress.Parse("1.2.3.4"),
                new NetworkAddressWithTime(NodeServiceFlags.NodeBloom, IPAddress.Parse("1.2.3.4"), 11, 0)
            };
        }
        [Theory]
        [MemberData(nameof(GetAddrCases))]
        public void GetSettingsMessages_Addr_Test(IPAddress ipToReturn, NetworkAddressWithTime expected)
        {
            var ns = new MockNodeStatus()
            {
                _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                _handShakeToSet = HandShakeState.Finished,
                updateTime = true,
                _protVer = Constants.P2PBip31ProtVer, // No ping, no feefilter, no sendheaders
            };
            var cs = new MockClientSettings()
            {
                _netType = NetworkType.MainNet,
                _catchup = false, // No getheaders
                _relay = true, // feefilter, addr
                myIpToReturn = ipToReturn,
                _services = NodeServiceFlags.NodeBloom,
                _port = 11
            };
            var rep = new ReplyManager(ns, cs);
            var msg = new Message(new VerackPayload(), NetworkType.MainNet);

            Message[] actual = rep.GetReply(msg);
            if (expected is null)
            {
                Assert.Null(actual);
            }
            else
            {
                Assert.Single(actual);
                Assert.True(actual[0].TryGetPayloadType(out PayloadType actualPlType));
                Assert.Equal(PayloadType.Addr, actualPlType);

                var actualPL = new AddrPayload();
                Assert.True(actualPL.TryDeserialize(new FastStreamReader(actual[0].PayloadData), out string error), error);
                Assert.Single(actualPL.Addresses);
                Assert.Equal(expected.NodeIP, actualPL.Addresses[0].NodeIP);
                Assert.Equal(expected.NodePort, actualPL.Addresses[0].NodePort);
                Assert.Equal(expected.NodeServices, actualPL.Addresses[0].NodeServices);
                Assert.NotEqual(0U, actualPL.Addresses[0].Time);
            }

            // Mock will change the following bool to false if it were called.
            Assert.False(ns.updateTime, "UpdateTime() was never called");

            // Mock either doesn't have any h.s. to set or if it did set h.s. it was checked and then turned to null
            Assert.Null(ns._handShakeToSet);

            // Mock will change the following bool to false if it were called.
            Assert.False(ns.updateTime, "UpdateTime() was never called");

            // Mock either doesn't have any h.s. to set or if it did set h.s. it was checked and then turned to null
            Assert.Null(ns._handShakeToSet);
        }


        public static IEnumerable<object[]> GetVersionCases()
        {
            var verPl = new VersionPayload();
            Assert.True(verPl.TryDeserialize(new FastStreamReader(Helper.HexToBytes("721101000100000000000000bc8f5e5400000000010000000000000000000000000000000000ffffc61b6409208d010000000000000000000000000000000000ffffcb0071c0208d128035cbc97953f80f2f5361746f7368693a302e392e332fcf05050001")), out string error), error);
            var mockIp = IPAddress.Parse("198.27.100.9");
            var cs = new MockClientSettings()
            {
                _protoVer = 123,
                _services = NodeServiceFlags.All,
                _time = new MockClientTime() { _now = 456, _updateTime = verPl.Timestamp },
                _port = 789,
                _ua = "foo",
                _relay = true,
                _netType = NetworkType.MainNet,
                _bchain = new MockBlockchain() { _height = 12345 },
                expUpdateAddr = mockIp
            };
            var verPlLowVer = new VersionPayload()
            {
                Version = 1,
                Services = verPl.Services,
                ReceivingNodeNetworkAddress = verPl.ReceivingNodeNetworkAddress,
                TransmittingNodeNetworkAddress = verPl.TransmittingNodeNetworkAddress,
                Nonce = verPl.Nonce,
                Timestamp = verPl.Timestamp,
                UserAgent = verPl.UserAgent,
                StartHeight = verPl.StartHeight,
                Relay = verPl.Relay
            };
            var msg = new Message(verPl, NetworkType.MainNet);
            var rcv = new NetworkAddress(NodeServiceFlags.NodeNone, mockIp, 444);
            var trs = new NetworkAddress(cs.Services, IPAddress.IPv6Any, 0);
            var verak = new Message(new VerackPayload(), NetworkType.MainNet);
            var ver = new Message(new VersionPayload(123, 456, rcv, trs, 0x0158a8e8ba5f3ed3, "foo", 12345, true), NetworkType.MainNet);
            var ping = new Message(new PingPayload(RngReturnValue), NetworkType.MainNet);


            yield return new object[]
            {
                new MockNodeStatus(verPl)
                {
                    _handShakeToReturn = HandShakeState.ReceivedAndReplied,
                    mediumViolation = true,
                    updateTime = true
                },
                cs, msg, null
            };
            yield return new object[]
            {
                new MockNodeStatus(verPl)
                {
                    _handShakeToReturn = HandShakeState.SentAndReceived,
                    mediumViolation = true,
                    updateTime = true
                },
                cs, msg, null
            };
            yield return new object[]
            {
                new MockNodeStatus(verPl)
                {
                    _handShakeToReturn = HandShakeState.Finished,
                    mediumViolation = true,
                    updateTime = true
                },
                cs, msg, null
            };

            yield return new object[]
            {
                new MockNodeStatus(verPl)
                {
                    _handShakeToReturn = HandShakeState.None,
                    _handShakeToSet = HandShakeState.ReceivedAndReplied,
                    _ip = mockIp,
                    _portToReturn = 444,
                    updateTime = true
                },
                cs, msg, new Message[] { verak, ver }
            };
            yield return new object[]
            {
                new MockNodeStatus(verPl)
                {
                    _handShakeToReturn = HandShakeState.Sent,
                    _handShakeToSet = HandShakeState.SentAndReceived,
                    updateTime = true
                },
                cs, msg, new Message[] { verak }
            };
            yield return new object[]
            {
                new MockNodeStatus(verPl)
                {
                    _handShakeToReturn = HandShakeState.SentAndConfirmed,
                    _handShakeToSet = HandShakeState.Finished,
                    updateTime = true,
                    expPingNonce = RngReturnValue
                },
                new MockClientSettings()
                {
                    _protoVer = cs._protoVer,
                    _services = cs._services,
                    _time = cs._time,
                    _port = cs._port,
                    _ua = cs._ua,
                    _netType = cs._netType,
                    expUpdateAddr = cs.expUpdateAddr,
                    _bchain = cs._bchain,
                    _relay = false, // No relay won't sent FeeFilter
                    _catchup = false,
                },
                msg, new Message[] { verak, ping }
            };
            yield return new object[]
            {
                new MockNodeStatus(verPlLowVer) // Protocol version is too low so the node is disconnected
                {
                    _handShakeToReturn = HandShakeState.SentAndConfirmed,
                    updateTime = true,
                    expectDiscSignal = true
                },
                cs,
                new Message(verPlLowVer, NetworkType.MainNet),
                null
            };

            // Settings messages are similar to CheckVerackTest
        }
        [Theory]
        [MemberData(nameof(GetVersionCases))]
        public void CheckVersionTest(MockNodeStatus ns, IClientSettings cs, Message msg, Message[] expected)
        {
            var rep = new ReplyManager(ns, cs)
            {
                rng = new MockNonceRng(RngReturnValue)
            };

            Message[] actual = rep.GetReply(msg);

            if (expected is null)
            {
                Assert.Null(actual);
            }
            else
            {
                Assert.NotNull(actual);
                Assert.Equal(expected.Length, actual.Length);
                for (int i = 0; i < expected.Length; i++)
                {
                    var actualStream = new FastStream(Constants.MessageHeaderSize + actual[i].PayloadData.Length);
                    var expectedStream = new FastStream(Constants.MessageHeaderSize + expected[i].PayloadData.Length);
                    actual[i].Serialize(actualStream);
                    expected[i].Serialize(expectedStream);

                    Assert.Equal(expectedStream.ToByteArray(), actualStream.ToByteArray());
                }
            }

            // Mock will change the following bool to false if it were called.
            Assert.False(ns.updateTime, "UpdateTime() was never called");

            // Mock either doesn't have any h.s. to set or if it did set h.s. it was checked and then turned to null
            Assert.Null(ns._handShakeToSet);

            Assert.False(ns.expectDiscSignal, "SignalDisconnect() was never called.");
        }
    }
}
