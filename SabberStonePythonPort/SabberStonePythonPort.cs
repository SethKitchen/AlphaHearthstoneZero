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
	public static class Extensions
	{
		static Random random = new Random();

		public static T DeepClone<T>(this T obj)
		{
			using (var ms = new MemoryStream())
			{
				var formatter = new BinaryFormatter();
				formatter.Serialize(ms, obj);
				ms.Position = 0;

				return (T)formatter.Deserialize(ms);
			}
		}

		public static IEnumerable<T> Shuffle<T>(IEnumerable<T> sequence)
		{
			T[] retArray = sequence.ToArray();


			for (int i = 0; i < retArray.Length - 1; i += 1)
			{
				int swapIndex = random.Next(i, retArray.Length);
				if (swapIndex != i)
				{
					T temp = retArray[i];
					retArray[i] = retArray[swapIndex];
					retArray[swapIndex] = temp;
				}
			}

			return retArray;
		}
	}



	[Serializable]
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

		public SabberStonePythonPort GetDeepCopy()
		{
			return Extensions.DeepClone(this);
		}

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
			allCards=Extensions.Shuffle(allCards).ToList();
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
			List<Card> allCardsD = new List<Card>();
			for (int i = 0; i < allCards.Count; i++)
			{
				if (allCards[i].Rarity != Rarity.LEGENDARY)
				{
					allCardsD.Add(allCards[i].Clone());
					allCardsD.Add(allCards[i].Clone());
				}
				else
				{
					allCardsD.Add(allCards[i].Clone());
				}
			}
			allCardsD=Extensions.Shuffle(allCardsD).ToList();
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
			SabberStoneCore.Model.Entities.Controller p = game.CurrentPlayer;
			SabberStoneCore.Model.Zones.HandZone hand = op.HandZone;
			int opHandCount = hand.Count;
			for(int i=0; i<opHandCount; i++)
			{
				int id = hand[0].Id;
				hand.Remove(0);
				hand.Add(new Unknown(in op, PlaceHolder, id));
			}
			game.AuraUpdate();
			int opDeckCount = op.DeckZone.Count;
			for(int i=0; i<opDeckCount; i++)
			{
				int id = op.DeckZone[i].Id;
				op.DeckZone[i].ActivatedTrigger?.Remove();
				op.DeckZone.Remove(0);
				op.DeckZone.Add(new Unknown(in op, PlaceHolder, id));
			}
			int myDeckCount = p.DeckZone.Count;
			for (int i = 0; i < myDeckCount; i++)
			{
				int id = p.DeckZone[i].Id;
				p.DeckZone[i].ActivatedTrigger?.Remove();
				p.DeckZone.Remove(0);
				p.DeckZone.Add(new Unknown(in p, PlaceHolder, id));
			}
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
			PlayerTask option = options[action%options.Count];
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

		public static int GetValue()
		{
			if(AllVisibleGame.CurrentOpponent.PlayState==PlayState.WON)
			{
				return -1;
			}
			else
			{
				return 0;
			}
		}
	}
}
