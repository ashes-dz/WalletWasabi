using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NBitcoin;
using WalletWasabi.WabiSabi.Models.Decomposition;

namespace WalletWasabi.WabiSabi.Client
{
	public class AmountDecomposer
	{
		public AmountDecomposer(FeeRate feeRate, Money minAllowedOutputAmount, int outputSize, int availableVsize)
		{
			OutputSize = outputSize;
			FeeRate = feeRate;
			AvailableVsize = availableVsize;
			MinimumAmountPlusFee = minAllowedOutputAmount + OutputFee;
			StandardDenominationsPlusFee = StandardDenomination.Values
				.Select(x => x + OutputFee)
				.OrderByDescending(x => x)
				.ToImmutableArray();
		}

		public FeeRate FeeRate { get; }
		public int AvailableVsize { get; }
		public Money MinimumAmountPlusFee { get; }
		public Money OutputFee => FeeRate.GetFee(OutputSize);
		public int OutputSize { get; }
		private ImmutableArray<Money> StandardDenominationsPlusFee { get; }

		public IEnumerable<Money> Decompose(IEnumerable<Coin> myInputCoins, IEnumerable<Coin> allInputCoins)
		{
			var histogram = GetDenominationFrequency(allInputCoins);

			var denoms = histogram
				.Where(x => x.Value > 1)
				.OrderByDescending(x => x.Key)
				.Select(x => x.Key)
				.ToArray();

			var inputs = myInputCoins.Select(x => x.EffectiveValue(FeeRate));
			var remaining = inputs.Sum();
			var remainingVsize = AvailableVsize;

			List<Money> outputAmounts = new();
			bool end = false;
			foreach (var denom in denoms.Where(x => x <= remaining))
			{
				while (denom <= remaining)
				{
					if (remaining < MinimumAmountPlusFee || remainingVsize < 2 * OutputSize)
					{
						end = true;
						break;
					}

					outputAmounts.Add(denom - OutputFee);
					remaining -= denom;
					remainingVsize -= OutputSize;
				}

				if (end)
				{
					break;
				}
			}

			if (remaining >= MinimumAmountPlusFee)
			{
				outputAmounts.Add(remaining - OutputFee);
			}

			var bestSet = outputAmounts;
			for (int i = 0; i < 10_000; i++)
			{
				remaining = inputs.Sum();
				var currSet = new List<Money>();
				do
				{
					var selectableDenomPlusFees = denoms.Where(x => x <= remaining).ToList();
					var denomPlusFees = selectableDenomPlusFees.Skip(selectableDenomPlusFees.Count / 3).ToList();
					var denom = denomPlusFees.RandomElement();
					if (denom is null || remaining < MinimumAmountPlusFee )
					{
						break;
					}

					if (denom <= remaining)
					{
						currSet.Add(denom - OutputFee);
						remaining -= denom;
					}
				}
				while (currSet.Count < bestSet.Count);

				if (currSet.Count >= bestSet.Count)
				{
					continue;
				}

				if (remaining >= MinimumAmountPlusFee)
				{
					currSet.Add(remaining - OutputFee);
				}

				if (currSet.Count < bestSet.Count)
				{
					bestSet = currSet;
				}
			}

			return bestSet;
		}

		private Dictionary<Money, long> GetDenominationFrequency(IEnumerable<Coin> allInputCoins)
		{
			var secondLargestInput = allInputCoins.OrderByDescending(x => x.Amount).Skip(1).FirstOrDefault();
			IEnumerable<Money> demonsForBreakDown = StandardDenominationsPlusFee.Where(x => secondLargestInput is null || x <= secondLargestInput.EffectiveValue(FeeRate));

			Dictionary<Money, long> denomProbabilities = new();

			foreach (var input in allInputCoins)
			{
				foreach (var denom in BreakDown(input.Amount, demonsForBreakDown))
				{
					if (!denomProbabilities.TryAdd(denom, 1))
					{
						denomProbabilities[denom]++;
					}
				}
			}

			return denomProbabilities;
		}

		private IEnumerable<Money> BreakDown(long amount, IEnumerable<Money> denominations)
		{
			var remaining = amount;

			foreach (var denomPlusFee in denominations)
			{
				if (denomPlusFee < MinimumAmountPlusFee || remaining < MinimumAmountPlusFee)
				{
					break;
				}

				while (denomPlusFee <= remaining)
				{
					yield return denomPlusFee;
					remaining -= denomPlusFee;
				}
			}

			if (remaining >= MinimumAmountPlusFee)
			{
				yield return remaining;
			}
		}
	}
}
