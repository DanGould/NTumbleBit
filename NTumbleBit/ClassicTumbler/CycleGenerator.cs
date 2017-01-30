﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NTumbleBit.ClassicTumbler
{
	public class OverlappedCycleGenerator
	{
		public OverlappedCycleGenerator()
		{
			FirstCycle = new CycleParameters();
			RegistrationOverlap = 1;
		}

		public int RegistrationOverlap
		{
			get; set;
		}

		public CycleParameters FirstCycle
		{
			get; set;
		}

		public CycleParameters GetRegistratingCycle(int blockHeight)
		{
			if(blockHeight < FirstCycle.Start)
				throw new InvalidOperationException("cycle generation starts at " + FirstCycle.Start);

			var registrationLength = FirstCycle.RegistrationDuration - RegistrationOverlap;
			var cycleCount = (blockHeight - FirstCycle.Start) / registrationLength;

			var cycle = FirstCycle.Clone();
			cycle.Start += registrationLength * cycleCount;
			return cycle;
		}

		public CycleParameters GetCycle(int startHeight)
		{
			if(startHeight < FirstCycle.Start)
				throw new InvalidOperationException("cycle generation starts at " + FirstCycle.Start + "(actual " + startHeight + ")");

			var registrationLength = FirstCycle.RegistrationDuration - RegistrationOverlap;
			if((startHeight - FirstCycle.Start) % registrationLength != 0)
				throw new InvalidOperationException("Invalid cycle start height");
			var result = FirstCycle.Clone();
			result.Start = startHeight;
			return result;
		}

		public CycleParameters GetPreviousCycle(CycleParameters cycle)
		{
			var registrationLength = FirstCycle.RegistrationDuration - RegistrationOverlap;
			if(registrationLength > cycle.Start)
				return null;
			return GetCycle(cycle.Start - registrationLength);
		}

		public CycleParameters GetNextCycle(CycleParameters cycle)
		{
			var registrationLength = FirstCycle.RegistrationDuration - RegistrationOverlap;
			return GetCycle(cycle.Start + registrationLength);
		}

		public CycleParameters[] GetCycles(int height)
		{
			List<CycleParameters> cycles = new List<CycleParameters>();
			var cycle = GetRegistratingCycle(height);
			while(cycle.IsInside(height))
			{
				var prev = GetPreviousCycle(cycle);
				if(prev == null)
					break;
				cycle = prev;
			}
			do
			{
				cycles.Add(cycle);
				cycle = GetNextCycle(cycle);
			} while(cycle.IsInside(height));
			return cycles.ToArray();
		}
	}
}
