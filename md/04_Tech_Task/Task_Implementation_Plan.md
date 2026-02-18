# Optimization & Design Pattern Review Plan

## Goal
Identify areas in the `ICN_T2` codebase where design patterns can be applied to improve **optimization** (performance, memory usage) and **structure** (maintainability).

## Findings
1.  **Conditional Logic in ViewModels**: `CharacterViewModel.ResolveCharacterDescription` uses `if (_game.Name == "Yo-Kai Watch 3")` checks. This violates OCP (Open-Closed Principle).
2.  **Heavy Object Creation**: `CharacterViewModel` creates a full `CharacterWrapper` for every entity.
3.  **Manual Caching**: `YW2.cs` and `CharacterViewModel` implement valid but manual caching mechanisms.

## Proposed Design Patterns

### 1. Strategy Pattern (Maintainability)
**Problem**: `ResolveCharacterDescription` contains hardcoded game-specific logic.
**Solution**: Extract text resolution into a `IGameTextStrategy`.
- `YW2` class provides a `YW2TextStrategy`.
- `YW3` (if exists) provides its own.
- `CharacterViewModel` simply calls `_game.TextStrategy.ResolveDescription(chara)`.

### 2. Virtual Proxy Pattern (Performance)
**Problem**: `CharacterWrapper` objects are created for all 1000+ entries, holding references to models and efficiently handling property changes, but potentially heavy if they subscribe to too much.
**Solution**:
- Ensure `CharacterWrapper` is lightweight.
- Use a **Virtual Proxy** for the heavy `Icon` image. Currently, it's lazy-loaded via an `async` batch job, which is a form of this, but it can be formalized.
- Load detailed properties (Unk fields) only when the item is `Selected`.

### 3. Flyweight Pattern (Memory)
**Problem**: Repeated string keys or shared assets.
**Solution**:
- The current `SharedCharacterIconCache` *is* a Flyweight implementation for images. It looks good.
- Ensure recurring strings (Tribe names, Ranks) are interned or shared.

## Recommended Action
I recommend starting with the **Strategy Pattern** refactoring to clean up the `CharacterViewModel` logic, as it improves code structure immediately and prepares for future game support.

# Plan: Refactor Text Resolution to Strategy Pattern

## 1. Define Strategy Interface
Create `ICN_T2\YokaiWatch\Games\Strategies\IGameTextStrategy.cs`:
```csharp
public interface IGameTextStrategy
{
    string ResolveName(CharaBase chara, bool isYokai);
    string ResolveDescription(CharaBase chara);
}
```

## 2. Implement Concrete Strategies
- `YW2TextStrategy` (Moves logic from `CharacterViewModel`)

## 3. Integrate
- Add `IGameTextStrategy TextStrategy { get; }` to `IGame`.
- Update `CharacterViewModel` to use it.
