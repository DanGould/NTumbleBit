﻿using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NTumbleBit.Services.RPC
{
	public class RPCWalletEntry
	{
		public uint256 TransactionId
		{
			get; set;
		}
		public int Confirmations
		{
			get; set;
		}
	}

	/// <summary>
	/// Workaround around slow Bitcoin Core RPC. 
	/// We are refreshing the list of received transaction once per block.
	/// </summary>
	public class RPCWalletCache
	{
		private readonly RPCClient _RPCClient;
		private readonly IRepository _Repo;
		public RPCWalletCache(RPCClient rpc, IRepository repository)
		{
			if(rpc == null)
				throw new ArgumentNullException("rpc");
			if(repository == null)
				throw new ArgumentNullException("repository");
			_RPCClient = rpc;
			_Repo = repository;
		}

		volatile uint256 _RefreshedAtBlock;

		public void Refresh(uint256 currentBlock)
		{
			if(_RefreshedAtBlock != currentBlock)
			{
				var newBlockCount = _RPCClient.GetBlockCount();
				//If we just udpated the value...
				if(Interlocked.Exchange(ref _BlockCount, newBlockCount) != newBlockCount)
				{
					_RefreshedAtBlock = currentBlock;
					ListTransactions();
				}
			}
		}

		int _BlockCount;
		public int BlockCount
		{
			get
			{
				if(_BlockCount == 0)
				{
					_BlockCount = _RPCClient.GetBlockCount();
				}
				return _BlockCount;
			}
		}

		public Transaction GetTransaction(uint256 txId)
		{
			var cached = GetCachedTransaction(txId);
			if(cached != null)
				return cached;
			var tx = FetchTransaction(txId);
			if(tx == null)
				return null;
			PutCached(tx);
			return tx;
		}

		ConcurrentDictionary<uint256, Transaction> _TransactionsByTxId = new ConcurrentDictionary<uint256, Transaction>();


		private Transaction FetchTransaction(uint256 txId)
		{
			try
			{
				//check in the wallet tx
				var result = _RPCClient.SendCommandNoThrows("gettransaction", txId.ToString(), true);
				if(result == null || result.Error != null)
				{
					//check in the txindex
					result = _RPCClient.SendCommandNoThrows("getrawtransaction", txId.ToString(), 1);
					if(result == null || result.Error != null)
						return null;
				}
				var tx = new Transaction((string)result.Result["hex"]);
				return tx;
			}
			catch(RPCException) { return null; }
		}

		public ICollection<RPCWalletEntry> GetEntries()
		{
			return _WalletEntries.Values;
		}

		private void PutCached(Transaction tx)
		{
			tx.CacheHashes();
			_Repo.UpdateOrInsert("CachedTransactions", tx.GetHash().ToString(), tx, (a, b) => b);
			_TransactionsByTxId.TryAdd(tx.GetHash(), tx);
		}

		private Transaction GetCachedTransaction(uint256 txId)
		{

			Transaction tx = null;
			if(_TransactionsByTxId.TryGetValue(txId, out tx))
			{
				return tx;
			}
			var cached = _Repo.Get<Transaction>("CachedTransactions", txId.ToString());
			if(cached != null)
				_TransactionsByTxId.TryAdd(txId, cached);
			return cached;
		}


		ConcurrentDictionary<uint256, RPCWalletEntry> _WalletEntries = new ConcurrentDictionary<uint256, RPCWalletEntry>();
		void ListTransactions()
		{
			var removeFromCache = new HashSet<uint256>(_TransactionsByTxId.Values.Select(tx => tx.GetHash()));
			var removeFromWalletEntries = new HashSet<uint256>(_WalletEntries.Keys);

			int count = 100;
			int skip = 0;
			int highestConfirmation = 0;

			while(true)
			{
				var result = _RPCClient.SendCommand("listtransactions", "*", count, skip, true);
				skip += count;
				var transactions = (JArray)result.Result;
				foreach(var obj in transactions)
				{
					var entry = new RPCWalletEntry();
					entry.Confirmations = obj["confirmations"] == null ? 0 : (int)obj["confirmations"];
					entry.TransactionId = new uint256((string)obj["txid"]);
					removeFromCache.Remove(entry.TransactionId);
					removeFromWalletEntries.Remove(entry.TransactionId);
					_WalletEntries.AddOrUpdate(entry.TransactionId, entry, (k, existing) => entry);
					if(obj["confirmations"] != null)
					{
						highestConfirmation = Math.Max(highestConfirmation, (int)obj["confirmations"]);
					}
				}
				if(transactions.Count < count || highestConfirmation >= 1400)
					break;
			}
			foreach(var remove in removeFromCache)
			{
				Transaction opt;
				_TransactionsByTxId.TryRemove(remove, out opt);
			}
			foreach(var remove in removeFromWalletEntries)
			{
				RPCWalletEntry opt;
				_WalletEntries.TryRemove(remove, out opt);
			}
		}


		public void ImportTransaction(Transaction transaction, int confirmations)
		{
			PutCached(transaction);
			_WalletEntries.TryAdd(transaction.GetHash(), new RPCWalletEntry()
			{
				Confirmations = confirmations,
				TransactionId = transaction.GetHash()
			});
		}
	}
}
