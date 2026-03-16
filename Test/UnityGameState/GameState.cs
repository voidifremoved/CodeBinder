// Unity Game State - Shared logic for C# (Unity) and Java (Server)
// This file tests CodeBinder's ability to translate common game state patterns.

using System;
using System.Collections.Generic;

namespace UnityGameState;

/// <summary>
/// Represents the current phase of a game.
/// </summary>
public enum GamePhase
{
    WaitingForPlayers,
    Starting,
    InProgress,
    Paused,
    GameOver
}

/// <summary>
/// Represents the type of action a player can perform.
/// </summary>
public enum ActionType
{
    Move,
    Attack,
    Defend,
    UseItem,
    SkipTurn
}

/// <summary>
/// Represents an item that a player can hold.
/// </summary>
public class Item
{
    public string Name { get; set; }
    public int Power { get; set; }
    public bool IsConsumable { get; set; }

    public Item(string name, int power, bool isConsumable)
    {
        Name = name;
        Power = power;
        IsConsumable = isConsumable;
    }

    public int CalculateEffectivePower(int playerLevel)
    {
        return Power + (playerLevel * 2);
    }
}

/// <summary>
/// Interface for anything that can take damage.
/// </summary>
public interface IDamageable
{
    int Health { get; }
    int MaxHealth { get; }
    void TakeDamage(int amount);
    bool IsAlive();
}

/// <summary>
/// Represents a player in the game.
/// </summary>
public class Player : IDamageable
{
    private int _health;
    private int _maxHealth;
    private string _name;
    private int _level;
    private List<Item> _inventory;

    public Player(string name, int maxHealth, int level)
    {
        _name = name;
        _maxHealth = maxHealth;
        _health = maxHealth;
        _level = level;
        _inventory = new List<Item>();
    }

    public string Name
    {
        get { return _name; }
    }

    public int Health
    {
        get { return _health; }
    }

    public int MaxHealth
    {
        get { return _maxHealth; }
    }

    public int Level
    {
        get { return _level; }
    }

    public void TakeDamage(int amount)
    {
        if (amount < 0)
            return;

        _health = _health - amount;
        if (_health < 0)
            _health = 0;
    }

    public void Heal(int amount)
    {
        if (amount < 0)
            return;

        _health = _health + amount;
        if (_health > _maxHealth)
            _health = _maxHealth;
    }

    public bool IsAlive()
    {
        return _health > 0;
    }

    public void AddItem(Item item)
    {
        _inventory.Add(item);
    }

    public int GetInventoryCount()
    {
        return _inventory.Count;
    }

    public Item GetItem(int index)
    {
        return _inventory[index];
    }

    /// <summary>
    /// Calculate total power from all items in inventory.
    /// </summary>
    public int GetTotalItemPower()
    {
        int total = 0;
        for (int i = 0; i < _inventory.Count; i++)
        {
            total = total + _inventory[i].CalculateEffectivePower(_level);
        }
        return total;
    }
}

/// <summary>
/// Represents a single action taken during a turn.
/// </summary>
public class TurnAction
{
    public ActionType Type { get; set; }
    public int TargetPlayerIndex { get; set; }
    public int ItemIndex { get; set; }
    public int Value { get; set; }

    public TurnAction(ActionType type, int targetPlayerIndex, int value)
    {
        Type = type;
        TargetPlayerIndex = targetPlayerIndex;
        ItemIndex = -1;
        Value = value;
    }
}

/// <summary>
/// Core game state manager.
/// Manages players, turns, and game phase transitions.
/// </summary>
public class GameState
{
    private List<Player> _players;
    private GamePhase _phase;
    private int _currentTurnIndex;
    private int _turnNumber;
    private int _maxPlayers;

    public GameState(int maxPlayers)
    {
        _maxPlayers = maxPlayers;
        _players = new List<Player>();
        _phase = GamePhase.WaitingForPlayers;
        _currentTurnIndex = 0;
        _turnNumber = 0;
    }

    public GamePhase Phase
    {
        get { return _phase; }
    }

    public int TurnNumber
    {
        get { return _turnNumber; }
    }

    public int CurrentTurnIndex
    {
        get { return _currentTurnIndex; }
    }

    public int PlayerCount
    {
        get { return _players.Count; }
    }

    /// <summary>
    /// Add a player to the game. Returns true if successful.
    /// </summary>
    public bool AddPlayer(Player player)
    {
        if (_phase != GamePhase.WaitingForPlayers)
            return false;

        if (_players.Count >= _maxPlayers)
            return false;

        _players.Add(player);
        return true;
    }

    /// <summary>
    /// Start the game if enough players have joined.
    /// </summary>
    public bool StartGame()
    {
        if (_phase != GamePhase.WaitingForPlayers)
            return false;

        if (_players.Count < 2)
            return false;

        _phase = GamePhase.InProgress;
        _currentTurnIndex = 0;
        _turnNumber = 1;
        return true;
    }

    /// <summary>
    /// Get the player whose turn it currently is.
    /// </summary>
    public Player GetCurrentPlayer()
    {
        return _players[_currentTurnIndex];
    }

    /// <summary>
    /// Get a player by index.
    /// </summary>
    public Player GetPlayer(int index)
    {
        return _players[index];
    }

    /// <summary>
    /// Process a turn action. Returns true if the action was valid.
    /// </summary>
    public bool ProcessAction(TurnAction action)
    {
        if (_phase != GamePhase.InProgress)
            return false;

        Player currentPlayer = GetCurrentPlayer();
        if (!currentPlayer.IsAlive())
            return false;

        switch (action.Type)
        {
            case ActionType.Attack:
                return ProcessAttack(currentPlayer, action);
            case ActionType.Defend:
                return ProcessDefend(currentPlayer, action);
            case ActionType.Heal:
                currentPlayer.Heal(action.Value);
                AdvanceTurn();
                return true;
            case ActionType.SkipTurn:
                AdvanceTurn();
                return true;
            default:
                return false;
        }
    }

    private bool ProcessAttack(Player attacker, TurnAction action)
    {
        if (action.TargetPlayerIndex < 0 || action.TargetPlayerIndex >= _players.Count)
            return false;

        Player target = _players[action.TargetPlayerIndex];
        if (!target.IsAlive())
            return false;

        int damage = action.Value + attacker.GetTotalItemPower();
        target.TakeDamage(damage);

        // Check for game over - only one player alive
        int aliveCount = 0;
        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i].IsAlive())
                aliveCount++;
        }

        if (aliveCount <= 1)
        {
            _phase = GamePhase.GameOver;
        }
        else
        {
            AdvanceTurn();
        }

        return true;
    }

    private bool ProcessDefend(Player defender, TurnAction action)
    {
        // Defend heals for half the value
        int healAmount = action.Value / 2;
        defender.Heal(healAmount);
        AdvanceTurn();
        return true;
    }

    /// <summary>
    /// Advance to the next living player's turn.
    /// </summary>
    private void AdvanceTurn()
    {
        if (_phase != GamePhase.InProgress)
            return;

        int startIndex = _currentTurnIndex;
        do
        {
            _currentTurnIndex = (_currentTurnIndex + 1) % _players.Count;
        } while (!_players[_currentTurnIndex].IsAlive() && _currentTurnIndex != startIndex);

        if (_currentTurnIndex == 0)
            _turnNumber++;
    }

    /// <summary>
    /// Find the winner (when game is over). Returns null if no winner yet.
    /// </summary>
    public Player FindWinner()
    {
        if (_phase != GamePhase.GameOver)
            return null;

        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i].IsAlive())
                return _players[i];
        }

        return null;
    }
}
