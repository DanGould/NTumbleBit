﻿using System;

using NTumbleBit.BouncyCastle.Crypto;
using NTumbleBit.BouncyCastle.Math;

namespace NTumbleBit.BouncyCastle.Crypto.Parameters
{
	internal class RsaKeyParameters
		: AsymmetricKeyParameter
	{
		private readonly BigInteger modulus;
		private readonly BigInteger exponent;

		public RsaKeyParameters(
			bool isPrivate,
			BigInteger modulus,
			BigInteger exponent)
			: base(isPrivate)
		{
			if(modulus == null)
				throw new ArgumentNullException(nameof(modulus));
			if(exponent == null)
				throw new ArgumentNullException(nameof(exponent));
			if(modulus.SignValue <= 0)
				throw new ArgumentException("Not a valid RSA modulus", nameof(modulus));
			if(exponent.SignValue <= 0)
				throw new ArgumentException("Not a valid RSA exponent", nameof(exponent));

			this.modulus = modulus;
			this.exponent = exponent;
		}

		public BigInteger Modulus
		{
			get
			{
				return modulus;
			}
		}

		public BigInteger Exponent
		{
			get
			{
				return exponent;
			}
		}

		public override bool Equals(
			object obj)
		{
			RsaKeyParameters kp = obj as RsaKeyParameters;

			if(kp == null)
			{
				return false;
			}

			return kp.IsPrivate == IsPrivate
				&& kp.Modulus.Equals(modulus)
				&& kp.Exponent.Equals(exponent);
		}

		public override int GetHashCode()
		{
			return modulus.GetHashCode() ^ exponent.GetHashCode() ^ IsPrivate.GetHashCode();
		}
	}
}