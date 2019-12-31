using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStonePythonPortNS
{
	public class calculate
	{
		public int Add(int a, int b)
		{
			return a + b;
		}

		public int Sub(int a, int b)
		{
			return a - b;
		}
	}

	public class SabberStonePythonPort
	{

		private static int CurrentPlayer { get; set; }
		private static Random r = new Random();
		private static CardClass Player1Class { get; set; }
		private static CardClass Player2Class { get; set; }
		private static List<Card> Player1Deck { get; set; }
		private static List<Card> Player2Deck { get; set; }
		private static GameConfig GameConfiguration { get; set; }
		private static Game AllVisibleGame { get; set; }
		private static Game PlayerViewGame { get; set; }
		private static byte[] PlayerViewGameBinary { get; set; }
		private static int NumberOfActions { get; set; }
		private static int PlayerTurn { get; set; }


		private static CardClass GetClassFromIndex(int index)
		{
			if (index == 0)
			{
				return CardClass.DRUID;
			}
			else if
				(index == 1)
			{
				return CardClass.HUNTER;
			}
			else if
				(index == 2)
			{
				return CardClass.MAGE;
			}
			else if
				(index == 3)
			{
				return CardClass.PALADIN;
			}
			else if
				(index == 4)
			{
				return CardClass.PRIEST;
			}
			else if
				(index == 5)
			{
				return CardClass.ROGUE;
			}
			else if
				(index == 6)
			{
				return CardClass.SHAMAN;
			}
			else if
				(index == 7)
			{
				return CardClass.WARLOCK;
			}
			else
			{
				return CardClass.WARRIOR;
			}
		}

		private static void Shuffle<T>(ref List<T> list)
		{
			var provider = new RNGCryptoServiceProvider();
			int n = list.Count;
			while (n > 1)
			{
				byte[] box = new byte[1];
				do provider.GetBytes(box);
				while (!(box[0] < n * (Byte.MaxValue / n)));
				int k = (box[0] % n);
				n--;
				T value = list[k];
				list[k] = list[n];
				list[n] = value;
			}
		}

		private static List<Card> GetRandomDeckFromClass(CardClass heroClass)
		{
			var allCards = Cards.All.ToList();
			for (int i = 0; i < allCards.Count; i++)
			{
				if (allCards[i].Rarity != Rarity.LEGENDARY)
				{
					allCards.Add(allCards[i]);
				}
			}
			Shuffle<Card>(ref allCards);
			var toReturn = new List<Card>();
			while (toReturn.Count < 30)
			{
				if (allCards[0].Class == heroClass || allCards[0].Class == CardClass.NEUTRAL)
				{
					toReturn.Add(allCards[0]);
				}
				allCards.RemoveAt(0);
			}

			return toReturn;
		}

		private static List<Card> GetRandomStandardDeckFromClass(CardClass heroClass)
		{
			var allCards = Cards.Standard[heroClass];
			allCards = allCards.Concat(Cards.Standard[CardClass.NEUTRAL]).ToList();
			List<Card> allCardsD = new List<Card>();
			for (int i = 0; i < allCards.Count; i++)
			{
				if (allCards[i].Rarity != Rarity.LEGENDARY)
				{
					allCardsD.Add(allCards[i]);
					allCardsD.Add(allCards[i]);
				}
				else
				{
					allCardsD.Add(allCards[i]);
				}
			}
			Shuffle<Card>(ref allCardsD);
			var toReturn = new List<Card>();
			while (toReturn.Count < 30)
			{
				toReturn.Add(allCardsD[0]);
				allCardsD.RemoveAt(0);
			}

			return toReturn;
		}

		private static readonly Dictionary<GameTag, int> PlaceHolder = new Dictionary<GameTag, int>(0);
		private static Game CreatePartiallyObservableGame(Game fullGame)
		{
			Game game = fullGame.Clone();
			SabberStoneCore.Model.Entities.Controller op = game.CurrentOpponent;
			SabberStoneCore.Model.Zones.HandZone hand = op.HandZone;
			ReadOnlySpan<IPlayable> span = hand.GetSpan();
			for (int i = span.Length - 1; i >= 0; --i)
			{
				hand.Remove(span[i]);
				hand.Add(new Unknown(in op, PlaceHolder, span[i].Id));
			}
			game.AuraUpdate();
			span = op.DeckZone.GetSpan();
			for (int i = 0; i < span.Length; i++)
				span[i].ActivatedTrigger?.Remove();
			var deck = new SabberStoneCore.Model.Zones.DeckZone(op);
			for (int i = 0; i < span.Length; i++)
			{
				span[i].ActivatedTrigger?.Remove();
				deck.Add(new Unknown(in op, PlaceHolder, span[i].Id));
			}
			op.DeckZone = deck;
			return game;
		}

		public static Func<List<IPlayable>, List<int>> MulliganRule()
		{
			return p => p.Where(t => t.Cost > 3).Select(t => t.Id).ToList();
		}

		public static byte[] ObjectToByteArray(object obj)
		{
			if (obj == null)
				return null;
			var bf = new BinaryFormatter();
			using (var ms = new MemoryStream())
			{
				bf.Serialize(ms, obj);
				return ms.ToArray();
			}
		}

		public static void Init()
		{
			int randomClassPlayer1 = r.Next() % 9;
			Player1Class = GetClassFromIndex(randomClassPlayer1);
			int randomClassPlayer2 = r.Next() % 9;
			Player2Class = GetClassFromIndex(randomClassPlayer2);
			Player1Deck = GetRandomStandardDeckFromClass(Player1Class);
			Player2Deck = GetRandomStandardDeckFromClass(Player2Class);
			GameConfiguration = new GameConfig()
			{
				StartPlayer = -1,
				Player1Name = "Player 1",
				Player1HeroClass = Player1Class,
				Player1Deck = Player1Deck,
				Player2Name = "Player 2",
				Player2HeroClass = Player2Class,
				Player2Deck = Player2Deck,
				FillDecks = false,
				Shuffle = true,
				SkipMulligan = false,
				Logging = false,
				History = false
			};
			AllVisibleGame = new Game(GameConfiguration);
			AllVisibleGame.StartGame();
			AllVisibleGame.Process(ChooseTask.Mulligan(AllVisibleGame.Player1, MulliganRule().Invoke(AllVisibleGame.Player1.Choice.Choices.Select(p => AllVisibleGame.IdEntityDic[p]).ToList())));
			AllVisibleGame.Process(ChooseTask.Mulligan(AllVisibleGame.Player2, MulliganRule().Invoke(AllVisibleGame.Player2.Choice.Choices.Select(p => AllVisibleGame.IdEntityDic[p]).ToList())));
			AllVisibleGame.MainReady();

			/*while (game.State != State.COMPLETE)
			{
				List<PlayerTask> options = game.CurrentPlayer.Options();
				PlayerTask option = options[Rnd.Next(options.Count)];
				//Console.WriteLine(option.FullPrint());
				game.Process(option);


			}
				turns += game.Turn;
				if (game.Player1.PlayState == PlayState.WON)
					wins[0]++;
				if (game.Player2.PlayState == PlayState.WON)
					wins[1]++;

			}
			watch.Stop();

			Console.WriteLine($"{total} games with {turns} turns took {watch.ElapsedMilliseconds} ms => " +
							  $"Avg. {watch.ElapsedMilliseconds / total} per game " +
							  $"and {watch.ElapsedMilliseconds / (total * turns)} per turn!");
			Console.WriteLine($"playerA {wins[0] * 100 / total}% vs. playerB {wins[1] * 100 / total}%!");*/
			PlayerViewGame = CreatePartiallyObservableGame(AllVisibleGame);
			PlayerViewGameBinary = ObjectToByteArray(PlayerViewGame);
			List<PlayerTask> options = AllVisibleGame.CurrentPlayer.Options();
			NumberOfActions = options.Count;
			PlayerTurn = AllVisibleGame.CurrentPlayer.Id;
		}

		public static void Reset()
		{
			Init();
		}

		public static void Step(int action)
		{
			List<PlayerTask> options = AllVisibleGame.CurrentPlayer.Options();
			PlayerTask option = options[action];
			AllVisibleGame.Process(option);
			PlayerViewGame = CreatePartiallyObservableGame(AllVisibleGame);
			PlayerViewGameBinary = ObjectToByteArray(PlayerViewGame);
			options = AllVisibleGame.CurrentPlayer.Options();
			NumberOfActions = options.Count;
			PlayerTurn = AllVisibleGame.CurrentPlayer.Id;
		}

		public static byte[] GetPlayerViewGameBinary()
		{
			return PlayerViewGameBinary;
		}

		public static int GetNumActions()
		{
			return NumberOfActions;
		}

		public static int GetPlayerTurn()
		{
			return PlayerTurn;
		}

		public static bool GetDone()
		{
			return AllVisibleGame.State == State.COMPLETE;

		}

		public static int[] GetRewardValue()
		{
			var currentPlayerBoard = AllVisibleGame.CurrentPlayer.BoardZone;
			var currentPlayerHand = AllVisibleGame.CurrentPlayer.HandZone;
			var currentPlayerSecret = AllVisibleGame.CurrentPlayer.SecretZone;
			var currentPlayerScore = 0;
			for (int i = 0; i < currentPlayerBoard.Count; i++)
			{
				if (currentPlayerBoard[i] != null)
				{
					currentPlayerScore += currentPlayerBoard[i].AttackDamage;
					currentPlayerScore += currentPlayerBoard[i].Health;
				}
			}
			for (int i = 0; i < currentPlayerHand.Count; i++)
			{
				if (currentPlayerHand[i] != null)
				{
					currentPlayerScore += 1;
				}
			}
			for (int i = 0; i < currentPlayerSecret.Count; i++)
			{
				if (currentPlayerSecret[i] != null)
				{
					currentPlayerScore += 1;
				}
			}

			currentPlayerScore += AllVisibleGame.CurrentPlayer.Hero.AttackDamage;
			currentPlayerScore += AllVisibleGame.CurrentPlayer.Hero.Health;
			currentPlayerScore += AllVisibleGame.CurrentPlayer.Hero.Armor;

			var oppPlayerBoard = AllVisibleGame.CurrentOpponent.BoardZone;
			var oppPlayerHand = AllVisibleGame.CurrentOpponent.HandZone;
			var oppPlayerSecret = AllVisibleGame.CurrentOpponent.SecretZone;
			var oppPlayerScore = 0;
			for (int i = 0; i < oppPlayerBoard.Count; i++)
			{
				if (oppPlayerBoard[i] != null)
				{
					oppPlayerScore += oppPlayerBoard[i].AttackDamage;
					oppPlayerScore += oppPlayerBoard[i].Health;
				}
			}
			for (int i = 0; i < oppPlayerHand.Count; i++)
			{
				if (oppPlayerHand[i] != null)
				{
					oppPlayerScore += 1;
				}
			}
			for (int i = 0; i < oppPlayerSecret.Count; i++)
			{
				if (oppPlayerSecret[i] != null)
				{
					oppPlayerScore += 1;
				}
			}

			oppPlayerScore += AllVisibleGame.CurrentOpponent.Hero.AttackDamage;
			oppPlayerScore += AllVisibleGame.CurrentOpponent.Hero.Health;
			oppPlayerScore += AllVisibleGame.CurrentOpponent.Hero.Armor;

			if (currentPlayerScore > oppPlayerScore)
			{
				return new int[] { 1, currentPlayerScore, oppPlayerScore };
			}
			else if (currentPlayerScore < oppPlayerScore)
			{
				return new int[] { -1, currentPlayerScore, oppPlayerScore };
			}
			else
			{
				return new int[] { 0, currentPlayerScore, oppPlayerScore };
			}
		}
	}
}
