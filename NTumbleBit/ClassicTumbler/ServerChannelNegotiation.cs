﻿using NBitcoin;
using NBitcoin.Crypto;
using NTumbleBit.PuzzlePromise;
using NTumbleBit.PuzzleSolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
	public class ServerChannelNegotiation
	{

		public ServerChannelNegotiation(ClassicTumblerParameters parameters, RsaKey tumblerKey, RsaKey voucherKey)
		{
			if(tumblerKey == null)
				throw new ArgumentNullException(nameof(tumblerKey));
			if(voucherKey == null)
				throw new ArgumentNullException(nameof(voucherKey));
			if(parameters.VoucherKey != voucherKey.PubKey)
				throw new ArgumentException("Voucher key does not match");
			if(parameters.ServerKey != tumblerKey.PubKey)
				throw new ArgumentException("Tumbler key does not match");
			TumblerKey = tumblerKey;
			VoucherKey = voucherKey;
			Parameters = parameters;
		}

		public RsaKey TumblerKey
		{
			get;
			private set;
		}
		public RsaKey VoucherKey
		{
			get;
			private set;
		}

		public ClassicTumblerParameters Parameters
		{
			get; set;
		}
	}

	public enum AliceServerChannelNegotiationStates
	{
		WaitingClientEscrowInformation,
		WaitingClientEscrow,
		Completed
	}

	public class AliceServerChannelNegotiation : ServerChannelNegotiation
	{
		private State InternalState
		{
			get; set;
		}

		public class State
		{
			public State()
			{
			}

			public PuzzleValue UnsignedVoucher
			{
				get; set;
			}
			public Key EscrowKey
			{
				get; set;
			}
			public PubKey OtherEscrowKey
			{
				get; set;
			}
			public PubKey RedeemKey
			{
				get; set;
			}
			public int CycleStart
			{
				get;
				set;
			}
			public AliceServerChannelNegotiationStates Status
			{
				get;
				set;
			}
		}

		public State GetInternalState()
		{
			var state =  Serializer.Clone(InternalState);
			return state;
		}

		public AliceServerChannelNegotiation(ClassicTumblerParameters parameters,
										RsaKey tumblerKey,
										RsaKey voucherKey) : base(parameters, tumblerKey, voucherKey)
		{
			if(parameters == null)
				throw new ArgumentNullException(nameof(parameters));
			InternalState = new State();
		}

		public AliceServerChannelNegotiation(ClassicTumblerParameters parameters,
										RsaKey tumblerKey,
										RsaKey voucherKey,
										State state) : base(parameters, tumblerKey, voucherKey)
		{
			if(parameters == null)
				throw new ArgumentNullException(nameof(parameters));
			InternalState = Serializer.Clone(state);
		}

		public void ReceiveClientEscrowInformation(ClientEscrowInformation escrowInformation, Key escrowKey)
		{
			AssertState(AliceServerChannelNegotiationStates.WaitingClientEscrowInformation);
			var cycle = Parameters.CycleGenerator.GetCycle(escrowInformation.Cycle);
			InternalState.CycleStart = cycle.Start;
			InternalState.EscrowKey = escrowKey;
			InternalState.OtherEscrowKey = escrowInformation.EscrowKey;
			InternalState.RedeemKey = escrowInformation.RedeemKey;
			InternalState.UnsignedVoucher = escrowInformation.UnsignedVoucher;
			InternalState.Status = AliceServerChannelNegotiationStates.WaitingClientEscrow;
		}

		public TxOut BuildEscrowTxOut()
		{
			return new TxOut(Parameters.Denomination + Parameters.Fee, CreateEscrowScript().Hash);
		}

		public Script CreateEscrowScript()
		{
			return EscrowScriptBuilder.CreateEscrow(new[] { InternalState.EscrowKey.PubKey, InternalState.OtherEscrowKey }, InternalState.RedeemKey, GetCycle().GetClientLockTime());
		}

		public SolverServerSession ConfirmClientEscrow(Transaction transaction, out PuzzleSolution solvedVoucher)
		{
			AssertState(AliceServerChannelNegotiationStates.WaitingClientEscrow);
			solvedVoucher = null;
			var escrow = CreateEscrowScript();
			var coin = transaction.Outputs.AsCoins().FirstOrDefault(txout => txout.ScriptPubKey == escrow.Hash.ScriptPubKey);
			if(coin == null)
				throw new PuzzleException("No output containing the escrowed coin");
			if(coin.Amount != Parameters.Denomination + Parameters.Fee)
				throw new PuzzleException("Incorrect amount");
			var voucher = InternalState.UnsignedVoucher;
			var escrowedCoin = coin.ToScriptCoin(escrow);

			var session = new SolverServerSession(TumblerKey, Parameters.CreateSolverParamaters());
			session.ConfigureEscrowedCoin(escrowedCoin, InternalState.EscrowKey);
			InternalState.UnsignedVoucher = null;
			InternalState.OtherEscrowKey = null;
			InternalState.RedeemKey = null;
			InternalState.EscrowKey = null;
			solvedVoucher = voucher.WithRsaKey(VoucherKey.PubKey).Solve(VoucherKey);
			InternalState.Status = AliceServerChannelNegotiationStates.Completed;
			return session;
		}

		public CycleParameters GetCycle()
		{
			return Parameters.CycleGenerator.GetCycle(InternalState.CycleStart);
		}

		public AliceServerChannelNegotiationStates Status
		{
			get
			{
				return InternalState.Status;
			}
		}

		private void AssertState(AliceServerChannelNegotiationStates state)
		{
			if(state != Status)
				throw new InvalidOperationException("Invalid state, actual " + InternalState.Status + " while expected is " + state);
		}
	}

	public enum BobServerChannelNegotiationStates
	{
		WaitingBobEscrowInformation,
		WaitingSignedTransaction,
		Completed
	}
	public class BobServerChannelNegotiation : ServerChannelNegotiation
	{
		private State InternalState
		{
			get; set;
		}

		public class State
		{
			public State()
			{
			}
			public Key RedeemKey
			{
				get; set;
			}
			public Key EscrowKey
			{
				get; set;
			}
			public int CycleStart
			{
				get;
				set;
			}

			public BobServerChannelNegotiationStates Status
			{
				get;
				set;
			}
			public PubKey OtherEscrowKey
			{
				get;
				set;
			}
		}

		public CycleParameters GetCycle()
		{
			return Parameters.CycleGenerator.GetCycle(InternalState.CycleStart);
		}

		public BobServerChannelNegotiation(ClassicTumblerParameters parameters,
										RsaKey tumblerKey,
										RsaKey voucherKey,
										int cycleStart) : base(parameters, tumblerKey, voucherKey)
		{
			if(parameters == null)
				throw new ArgumentNullException(nameof(parameters));
			InternalState = new State();
			InternalState.CycleStart = cycleStart;
		}

		public BobServerChannelNegotiation(ClassicTumblerParameters parameters,
										RsaKey tumblerKey,
										RsaKey voucherKey,
										State state) : base(parameters, tumblerKey, voucherKey)
		{
			if(parameters == null)
				throw new ArgumentNullException(nameof(parameters));
			InternalState = Serializer.Clone(state);
		}

		public State GetInternalState()
		{
			var state = Serializer.Clone(InternalState);
			return state;
		}

		public void ReceiveBobEscrowInformation(OpenChannelRequest openChannelRequest)
		{
			if(openChannelRequest == null)
				throw new ArgumentNullException($"{nameof(openChannelRequest)}");
			AssertState(BobServerChannelNegotiationStates.WaitingBobEscrowInformation);

			if(!VoucherKey.PubKey.Verify(openChannelRequest.Signature, NBitcoin.Utils.ToBytes((uint)openChannelRequest.CycleStart, true), openChannelRequest.Nonce))
				throw new PuzzleException("Invalid voucher");

			var escrow = new Key();
			var redeem = new Key();
			InternalState.EscrowKey = escrow;
			InternalState.OtherEscrowKey = openChannelRequest.EscrowKey;
			InternalState.RedeemKey = redeem;
			InternalState.Status = BobServerChannelNegotiationStates.WaitingSignedTransaction;
		}

		public TxOut BuildEscrowTxOut()
		{
			AssertState(BobServerChannelNegotiationStates.WaitingSignedTransaction);
			var escrowScript = CreateEscrowScript();
			return new TxOut(Parameters.Denomination, escrowScript.Hash);
		}

		private Script CreateEscrowScript()
		{
			return EscrowScriptBuilder.CreateEscrow(
				new[]
				{
					InternalState.EscrowKey.PubKey,
					InternalState.OtherEscrowKey
				},
				InternalState.RedeemKey.PubKey,
				GetCycle().GetTumblerLockTime());
		}

		public PromiseServerSession SetSignedTransaction(Transaction transaction)
		{
			AssertState(BobServerChannelNegotiationStates.WaitingSignedTransaction);
			var escrow = BuildEscrowTxOut();
			var output = transaction.Outputs.AsIndexedOutputs()
				.Single(o => o.TxOut.ScriptPubKey == escrow.ScriptPubKey && o.TxOut.Value == escrow.Value);
			var escrowedCoin = new Coin(output).ToScriptCoin(CreateEscrowScript());
			PromiseServerSession session = new PromiseServerSession(Parameters.CreatePromiseParamaters());
			session.ConfigureEscrowedCoin(escrowedCoin, InternalState.EscrowKey, InternalState.RedeemKey);
			InternalState.EscrowKey = null;
			InternalState.RedeemKey = null;
			InternalState.Status = BobServerChannelNegotiationStates.Completed;
			return session;
		}

		private void AssertState(BobServerChannelNegotiationStates state)
		{
			if(state != InternalState.Status)
				throw new InvalidOperationException("Invalid state, actual " + InternalState.Status + " while expected is " + state);
		}

		public UnsignedVoucherInformation GenerateUnsignedVoucher()
		{
			PuzzleSolution solution = null;
			var puzzle = Parameters.VoucherKey.GeneratePuzzle(ref solution);
			uint160 nonce;
			var cycle = GetCycle().Start;
			var signature = VoucherKey.Sign(NBitcoin.Utils.ToBytes((uint)cycle, true), out nonce);
			return new UnsignedVoucherInformation
			{
				CycleStart = cycle,
				Nonce = nonce,
				Puzzle = puzzle.PuzzleValue,
				EncryptedSignature = new XORKey(solution).XOR(signature)
			};
		}
	}
}
