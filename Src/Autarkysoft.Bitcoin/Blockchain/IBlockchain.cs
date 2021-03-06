﻿// Autarkysoft.Bitcoin
// Copyright (c) 2020 Autarkysoft
// Distributed under the MIT software license, see the accompanying
// file LICENCE or http://www.opensource.org/licenses/mit-license.php.

using Autarkysoft.Bitcoin.Blockchain.Blocks;
using System;

namespace Autarkysoft.Bitcoin.Blockchain
{
    /// <summary>
    /// Defines methods and properties that a blockchain (or database) manager implements.
    /// </summary>
    public interface IBlockchain
    {
        /// <summary>
        /// Returns the best block height (tip of the stored chain)
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Returns the current block height based on its previous block hash
        /// </summary>
        /// <param name="prevHash">Previous block hash</param>
        /// <returns>Block height (>=0) or -1 if previous block hash was not found in the database</returns>
        int FindHeight(ReadOnlySpan<byte> prevHash);

        /// <summary>
        /// Returns the next difficulty target based on the best stored chain.
        /// </summary>
        /// <returns>Next difficulty target</returns>
        Target GetNextTarget();

        /// <summary>
        /// Processes the given block by validating the header, transactions,... and adds it to the database.
        /// Should also handle forks and reorgs.
        /// The return value indicates evaluation success.
        /// </summary>
        /// <param name="block">Block to process</param>
        /// <returns>True if the block was valid; otherwise false.</returns>
        bool ProcessBlock(IBlock block);
        /// <summary>
        /// Process given block headers and update the header database
        /// </summary>
        /// <param name="headers">An array of block headers</param>
        /// <returns>Result of processing the given headers</returns>
        BlockProcessResult ProcessHeaders(BlockHeader[] headers);

        /// <summary>
        /// Returns an array of <see cref="BlockHeader"/>s from the tip to be used in 
        /// <see cref="P2PNetwork.Messages.MessagePayloads.GetHeadersPayload"/> for initial sync.
        /// </summary>
        /// <returns>An array of <see cref="BlockHeader"/> with at least 1 item.</returns>
        BlockHeader[] GetBlockHeaderLocator();
        /// <summary>
        /// Compares this client's local header list with the given hashes and will return headers that are missing.
        /// </summary>
        /// <param name="hashesToCompare">Header hashes to compare with local headers</param>
        /// <param name="stopHash">Hash of the header to stop at</param>
        /// <returns>An array of missing block headers</returns>
        BlockHeader[] GetMissingHeaders(byte[][] hashesToCompare, byte[] stopHash);
    }
}
