﻿// Autarkysoft.Bitcoin
// Copyright (c) 2020 Autarkysoft
// Distributed under the MIT software license, see the accompanying
// file LICENCE or http://www.opensource.org/licenses/mit-license.php.

using Autarkysoft.Bitcoin.Encoders;
using Autarkysoft.Bitcoin.P2PNetwork.Messages;
using Autarkysoft.Bitcoin.P2PNetwork.Messages.MessagePayloads;
using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Autarkysoft.Bitcoin.P2PNetwork
{
    /// <summary>
    /// Defines a P2P message manager used for handling send/receive P2P messages for nodes.
    /// </summary>
    public class MessageManager
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MessageManager"/> using the given parameters.
        /// </summary>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="ArgumentOutOfRangeException"/>
        /// <param name="bufferLength">Size of the buffer used for each <see cref="SocketAsyncEventArgs"/> object</param>
        /// <param name="versionMessage">Version message (used for initiating handshake)</param>
        /// <param name="repMan">A reply manager to create appropriate response to a given message</param>
        /// <param name="netType">[Default value = <see cref="NetworkType.MainNet"/>] Network type</param>
        public MessageManager(int bufferLength, Message versionMessage, IReplyManager repMan,
                              NetworkType netType = NetworkType.MainNet)
        {
            if (bufferLength <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferLength), "Buffer length can not be negative or zero.");
            if (repMan is null)
                throw new ArgumentNullException(nameof(repMan), "Reply manager can not be null.");

            magicBytes = netType switch
            {
                NetworkType.MainNet => Base16.Decode(Constants.MainNetMagic),
                NetworkType.TestNet => Base16.Decode(Constants.TestNetMagic),
                NetworkType.RegTest => Base16.Decode(Constants.RegTestMagic),
                _ => throw new ArgumentException(Err.InvalidNetwork)
            };
            this.netType = netType;

            verMsg = versionMessage;
            replyManager = repMan;

            buffLen = bufferLength;
            Init();
        }



        private const int HeaderLength = Message.MinSize;

        private readonly int buffLen;
        private readonly NetworkType netType;
        private readonly byte[] magicBytes;
        private readonly Message verMsg;
        private readonly IReplyManager replyManager;

        private byte[] _sendData;
        /// <summary>
        /// Gets or sets the data to be sent
        /// </summary>
        public byte[] DataToSend
        {
            get => _sendData;
            set
            {
                remainSend = value == null ? 0 : value.Length;
                sendOffset = 0;
                _sendData = value;
            }
        }
        private int sendOffset, remainSend;


        /// <summary>
        /// Indicates whether the previous message was fully received
        /// </summary>
        public bool IsReceiveCompleted { get; private set; }
        /// <summary>
        /// Indicates whether there is any data to send
        /// </summary>
        public bool HasDataToSend => remainSend > 0 || toSendQueue.Count > 0;
        /// <summary>
        /// Indicates whether the handshake process between two nodes is already performed and succeeded
        /// </summary>
        public bool IsHandShakeComplete { get; private set; }

        private byte[] tempHolder;

        private readonly Queue<Message> toSendQueue = new Queue<Message>();


        internal void Init()
        {
            DataToSend = null;
            toSendQueue.Clear();
            IsHandShakeComplete = false;
            IsReceiveCompleted = true;
        }

        /// <summary>
        /// Sets <see cref="SocketAsyncEventArgs"/>'s buffer for a send operation 
        /// (caller must check <see cref="HasDataToSend"/> before calling this)
        /// </summary>
        /// <param name="sendEventArgs">Socket arg to use</param>
        public void SetSendBuffer(SocketAsyncEventArgs sendEventArgs)
        {
            if (remainSend == 0)
            {
                Message msg = toSendQueue.Dequeue();
                FastStream stream = new FastStream((int)msg.payloadSize + HeaderLength);
                msg.Serialize(stream);
                DataToSend = stream.ToByteArray();
            }

            if (remainSend >= buffLen)
            {
                Buffer.BlockCopy(DataToSend, sendOffset, sendEventArgs.Buffer, 0, buffLen);
                sendEventArgs.SetBuffer(0, buffLen);

                sendOffset += buffLen;
                remainSend -= buffLen;
            }
            else if (remainSend < buffLen)
            {
                Buffer.BlockCopy(DataToSend, sendOffset, sendEventArgs.Buffer, 0, remainSend);
                sendEventArgs.SetBuffer(0, remainSend);

                sendOffset += remainSend;
                remainSend = 0;
            }
        }

        /// <summary>
        /// Starts the handshake process (called must check <see cref="IsHandShakeComplete"/> before calling this).
        /// </summary>
        /// <param name="srEventArgs">Socket arg to use</param>
        public void StartHandShake(SocketAsyncEventArgs srEventArgs)
        {
            toSendQueue.Enqueue(verMsg);
            SetSendBuffer(srEventArgs);
            IsHandShakeComplete = true;
        }


        private void SetRejectMessage(PayloadType plt, string error)
        {
            // TODO: set a bool to turn Reject response on/off
            Message rej = replyManager.GetReject(plt, error);
            toSendQueue.Enqueue(rej);
            IsReceiveCompleted = true;
        }

        private void ProcessData(FastStreamReader stream)
        {
            // This method is only called when magic bytes are already found and stream size is at least header size. 
            // Deserialize header and payload separately.
            Message msg = new Message(netType);
            if (msg.TryDeserializeHeader(stream, out string error))
            {
                if (stream.GetRemainingBytesCount() < msg.payloadSize)
                {
                    // Receive is not finished
                    tempHolder = new byte[HeaderLength + stream.GetRemainingBytesCount()];
                    Buffer.BlockCopy(msg.SerializeHeader(), 0, tempHolder, 0, HeaderLength);
                    _ = stream.TryReadByteArray(stream.GetRemainingBytesCount(), out byte[] remBa);
                    Buffer.BlockCopy(remBa, 0, tempHolder, HeaderLength, remBa.Length);
                    IsReceiveCompleted = false;
                }
                else if (msg.Payload.TryDeserialize(stream, out error) && msg.VerifyChecksum())
                {
                    Message reply = replyManager.GetReply(msg);
                    if (!(reply is null))
                    {
                        toSendQueue.Enqueue(reply);
                    }

                    // TODO: handle received message here. eg. write received block to disk,...

                    // Handle remaining data
                    if (stream.GetRemainingBytesCount() > 0)
                    {
                        IsReceiveCompleted = false;
                        _ = stream.TryReadByteArray(stream.GetRemainingBytesCount(), out tempHolder);
                    }
                    else
                    {
                        IsReceiveCompleted = true;
                        tempHolder = null;
                    }
                }
                else // There were enough bytes for payload but something failed => reject the message
                {
                    IsReceiveCompleted = true;
                    tempHolder = null;
                    SetRejectMessage(msg.Payload.PayloadType, error);
                }
            }
            else
            {
                IsReceiveCompleted = true;
                tempHolder = null;
                SetRejectMessage(msg.Payload?.PayloadType ?? 0, error);
            }
        }


        /// <summary>
        /// Reads and processes the given bytes from the given buffer and provided length.
        /// </summary>
        /// <param name="buffer">Buffer containing the received bytes</param>
        /// <param name="len">Number of bytes received</param>
        public void ReadBytes(byte[] buffer, int len)
        {
            if (len > 0 && buffer != null)
            {
                if (IsReceiveCompleted)
                {
                    FastStreamReader stream = new FastStreamReader(buffer, 0, len);
                    if (stream.FindAndSkip(magicBytes))
                    {
                        int rem = stream.GetRemainingBytesCount();
                        if (rem >= HeaderLength)
                        {
                            ProcessData(stream);
                        }
                        else if (rem != 0)
                        {
                            stream.TryReadByteArray(rem, out tempHolder);
                            IsReceiveCompleted = false;
                        }
                    }
                    // TODO: some sort of point system is needed to give negative points to malicious nodes
                    //       eg. sending garbage bytes which is what is caught in the "else" part of the "if" above
                }
                else
                {
                    byte[] temp = new byte[tempHolder.Length + len];
                    Buffer.BlockCopy(tempHolder, 0, temp, 0, tempHolder.Length);
                    Buffer.BlockCopy(buffer, 0, temp, tempHolder.Length, len);
                    tempHolder = temp;

                    if (tempHolder.Length >= HeaderLength)
                    {
                        IsReceiveCompleted = true;
                        ReadBytes(tempHolder, tempHolder.Length);
                    }
                }
            }
        }

        /// <summary>
        /// Reads and processes the bytes that were received on this <see cref="SocketAsyncEventArgs"/>.
        /// </summary>
        /// <param name="recEventArgs"><see cref="SocketAsyncEventArgs"/> to use</param>
        public void ReadBytes(SocketAsyncEventArgs recEventArgs) => ReadBytes(recEventArgs.Buffer, recEventArgs.BytesTransferred);
    }
}
