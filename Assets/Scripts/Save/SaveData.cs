using System;
using System.Collections.Generic;
using UnityEngine;

// What goes into a save file (JSON). Items and pickups are stored by id, never by asset reference.
[Serializable]
public class SaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public string savedAt;          // local date/time, ISO 8601
    public float playTime;          // seconds
    public string location;         // name of the save station
    public string scene;

    public Vector3 position;
    public float yaw;
    public int health;
    public List<SavedStack> inventory = new List<SavedStack>();
    public List<int> magazines = new List<int>();   // rounds in each loadout weapon
    public int selectedWeapon;
    public bool armed;
    public List<SavedStack> pickups = new List<SavedStack>();   // pickup save id -> what is left
    public int chips = -1;          // the operator's blackjack chips (-1 = not saved: keep the current amount)
    public string outfit;           // what she is wearing (outfit key; empty = keep the current one)
    public List<SavedEnemy> enemies = new List<SavedEnemy>();   // by save id; missing = as at the start of the scene
    public int coins = -1;          // casino prize coins (-1 = not saved: keep the current amount)
    public List<SceneWorld> worlds = new List<SceneWorld>();   // the other scenes she has been to (pickups, enemies)
    public List<string> ownedOutfits;   // prizes bought at the casino (null = not saved: keep the current ones)
    public List<string> storyFlags;     // what the operator has seen happen (files on his PC); null = not saved: keep
    public List<SavedStack> storyCounters;   // counted things (coffees...); null = not saved: keep
}

// how a scene was left: what is left of each pickup, the enemies (by save id)
[Serializable]
public class SceneWorld
{
    public string scene;
    public List<SavedStack> pickups = new List<SavedStack>();
    public List<SavedEnemy> enemies = new List<SavedEnemy>();
}

[Serializable]
public class SavedEnemy
{
    public string id;
    public bool dead;
    public int health;
    public Vector3 position;
    public float yaw;
    public bool fellBackward;
}

[Serializable]
public class SavedStack
{
    public string id;
    public int amount;

    public SavedStack() { }   // for the JSON serializer

    public SavedStack(string id, int amount)
    {
        this.id = id;
        this.amount = amount;
    }
}
