﻿// Autarkysoft.Bitcoin
// Copyright (c) 2020 Autarkysoft
// Distributed under the MIT software license, see the accompanying
// file LICENCE or http://www.opensource.org/licenses/mit-license.php.

namespace Autarkysoft.Bitcoin.P2PNetwork
{
    /// <summary>
    /// Different states of hand-shake with another peer
    /// </summary>
    public enum HandShakeState
    {
        /// <summary>
        /// The initial state (no connection made yet)
        /// </summary>
        None,
        /// <summary>
        /// This client received a <see cref="Messages.MessagePayloads.VersionPayload"/> and replied with
        /// <see cref="Messages.MessagePayloads.VerackPayload"/> and <see cref="Messages.MessagePayloads.VersionPayload"/>
        /// and is waiting for a <see cref="Messages.MessagePayloads.VerackPayload"/> response.
        /// </summary>
        ReceivedAndReplied,
        /// <summary>
        /// This client sent the <see cref="Messages.MessagePayloads.VersionPayload"/> and is waiting for reply
        /// </summary>
        Sent,
        /// <summary>
        /// This client sent the <see cref="Messages.MessagePayloads.VersionPayload"/> and received 
        /// <see cref="Messages.MessagePayloads.VerackPayload"/> but not <see cref="Messages.MessagePayloads.VersionPayload"/>.
        /// </summary>
        SentAndConfirmed,
        /// <summary>
        /// This client sent the <see cref="Messages.MessagePayloads.VersionPayload"/> and received 
        /// <see cref="Messages.MessagePayloads.VersionPayload"/> but not <see cref="Messages.MessagePayloads.VerackPayload"/>.
        /// </summary>
        SentAndReceived,
        /// <summary>
        /// Handshake is finished no more <see cref="Messages.MessagePayloads.VersionPayload"/> or
        /// <see cref="Messages.MessagePayloads.VerackPayload"/> should be sent or received
        /// </summary>
        Finished
    }
}
