﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see licence.txt in the main folder

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ArcheAgeGame.ArcheAge.World
{
	public class WorldManager
	{
		public const int HeartbeatTime = 500;
		public const int Second = 1000, Minute = Second * 60, Hour = Minute * 60;

		private int _handles;

		// These are object id range starting points. The skill objects I
		// saw in-game so far were above 0x54B600000000 for example,
		// while the session and ability objects were above 0xE1A900000000.
		// In some games, ids have to to be in specific ranges, or nothing
		// will work. You might not be able to login, or interact with the
		// world, etc. I haven't seen any indication that this is the case in
		// ToS yet, but I'd rather be on the safe side for now, until we can
		// confirm that it's unnecessary.
		private long _sessionObjectIds = 0x0000E1A900000000;
		private long _skillObjectIds = 0x000054B600000000;

		private Dictionary<int, Map> _mapsId;
		private Dictionary<string, Map> _mapsName;
		private object _mapsLock = new object();

		private Timer _heartbeatTimer;

		/// <summary>
		/// Returns the amount of maps in the world.
		/// </summary>
		public int Count { get { lock (this._mapsLock) return this._mapsId.Count; } }

		/// <summary>
		/// Creates new world manager.
		/// </summary>
		public WorldManager()
		{
			this._mapsId = new Dictionary<int, Map>();
			this._mapsName = new Dictionary<string, Map>();
		}

		/// <summary>
		/// Initializes heartbeat timer.
		/// </summary>
		private void SetUpHeartbeat()
		{
			var now = DateTime.Now;

			// Start timer on the next HeartbeatTime
			// (eg on the next full 500 ms) and run it regularly afterwards.
			this._heartbeatTimer = new Timer(this.Heartbeat, null, HeartbeatTime - (now.Ticks / 10000 % HeartbeatTime), HeartbeatTime);
		}

		/// <summary>
		/// Handles regularly occuring events and raises time events.
		/// </summary>
		/// <remarks>
		/// On the first call all time events are raised,
		/// because lastHeartbeat is 0, and the events depend on the time
		/// since the last heartbeat. This also ensures that they aren't
		/// called multiple times.
		/// </remarks>
		private void Heartbeat(object _)
		{
			this.UpdateEntities();
		}

		/// <summary>
		/// Updates all entities on all maps.
		/// </summary>
		private void UpdateEntities()
		{
			lock (this._mapsLock)
			{
				foreach (var map in this._mapsId.Values)
					map.UpdateEntities();
			}
		}

		/// <summary>
		/// Returns a new handle to be used for a character or monster.
		/// </summary>
		/// <returns></returns>
		public int CreateHandle()
		{
			// TODO: 2b are a lot, but if items use this as well... we might
			//   need a fallback, or a way to release handles of logged out
			//   characters or killed monsters.
			return Interlocked.Increment(ref this._handles);
		}

		/// <summary>
		/// Returns a new object id that can be used for a session object.
		/// </summary>
		/// <returns></returns>
		public long CreateSessionObjectId()
		{
			// XXX: How exactly do these unique ids work? Do they all come
			// from the same pool? Are there ranges? Do they have to be
			// unique between multiple types? Can we use one getter for
			// all "objects"?
			return Interlocked.Increment(ref this._sessionObjectIds);
		}

		/// <summary>
		/// Returns a new object id that can be used for a skill object.
		/// </summary>
		/// <returns></returns>
		public long CreateSkillObjectId()
		{
			return Interlocked.Increment(ref this._skillObjectIds);
		}

		/// <summary>
		/// Initializes world.
		/// </summary>
		public void Initialize()
		{
			foreach (var entry in ArcheAgeGame.Instance.Data.MapDb.Entries.Values)
			{
				var map = new Map(entry.Id, entry.ClassName);
				this._mapsId.Add(map.Id, map);
				this._mapsName.Add(map.Name, map);
			}

			this.SetUpHeartbeat();
		}

		/// <summary>
		/// Returns map by id, or null if it doesn't exist.
		/// </summary>
		/// <param name="mapId"></param>
		public Map GetMap(int mapId)
		{
			Map result;
			lock (this._mapsLock) this._mapsId.TryGetValue(mapId, out result);
			return result;
		}

		/// <summary>
		/// Returns map by name, or null if it doesn't exist.
		/// </summary>
		/// <param name="mapName"></param>
		public Map GetMap(string mapName)
		{
			Map result;
			lock (this._mapsLock) this._mapsName.TryGetValue(mapName, out result);
			return result;
		}

		/// <summary>
		/// Removes all scripted entities, like NPCs.
		/// </summary>
		public void RemoveScriptedEntities()
		{
			lock (this._mapsLock)
			{
				foreach (var map in this._mapsId.Values)
					map.RemoveScriptedEntities();
			}
		}

		/// <summary>
		/// Returns the first character found with the given team name,
		/// or null if none were found.
		/// </summary>
		public Character GetCharacterByTeamName(string teamName)
		{
			lock (this._mapsLock)
			{
				foreach (var map in this._mapsId.Values)
				{
					var character = map.GetCharacterByTeamName(teamName);
					if (character != null)
						return character;
				}
			}

			return null;
		}

		/// <summary>
		/// Returns all Characters that are currently online.
		/// </summary>
		public Character[] GetCharacters()
		{
			lock (this._mapsLock)
				return this._mapsId.Values.SelectMany(a => a.GetCharacters()).ToArray();
		}

		/// <summary>
		/// Returns all online characters that match the given predicate.
		/// </summary>
		public Character[] GetCharacters(Func<Character, bool> predicate)
		{
			lock (this._mapsLock)
				return this._mapsId.Values.SelectMany(a => a.GetCharacters(predicate)).ToArray();
		}
	}
}