# Turnbase: A Provably Fair Gaming System

Turnbase is a robust, transparent, and provably fair gaming platform for turn-based games. It provides a framework for creating and managing fair online gaming experiences where players can verify the integrity of gameplay.

## 🌟 Features

- **Provably Fair Algorithm**: Cryptographic verification ensures game outcomes cannot be manipulated
- **Flexible Game Rules**: Implement any turn-based game through the `IGameRule` interface
- **Real-time Gameplay**: Built with SignalR for seamless multiplayer experiences
- **State Management**: Complete tracking of game states for verification and replay
- **API-First Design**: RESTful endpoints for game creation, state retrieval, and move submission
- **Database Persistence**: Secure storage of all game states and moves

## 🏗️ Architecture

Turnbase consists of several key components:

### Core Components

- **FairnessService**: Handles the cryptographic verification of game states and moves
- **GameHub**: SignalR hub for real-time game updates and player interactions
- **GameContext**: Database context for storing game states and player actions
- **IGameRule**: Interface for implementing game-specific rules and logic

### Project Structure

- **Turnbase.Server**: The main server application hosting the API and SignalR hub
- **Turnbase.Rules**: Game-specific rule implementations (e.g., Scrabble)
- **Turnbase.Tests**: Test suite to ensure correct behavior of the fairness algorithms

## 🎲 How Provable Fairness Works

Turnbase employs a commitment-reveal scheme for provably fair gameplay:

1. **Game Initialization**: Server generates and commits to a random seed
2. **Player Actions**: Each move is signed and verified against the committed state
3. **State Transition**: Server applies moves according to the game rules, updating the game state
4. **Verification**: Players can independently verify each state transition using the published rules and cryptographic proofs

This system ensures that neither players nor the server can manipulate game outcomes after the game has begun.

## 🎮 Implemented Games

### Scrabble

A full implementation of the classic word game with:

- Standard 15x15 board
- Dictionary word validation
- Score calculation based on letter values
- Proper turn management and tile distribution
- Complete ruleset enforcement (placement, connectivity, etc.)

## 🚀 Getting Started

### Prerequisites

- .NET 8.0 SDK or later

### Setup and Installation

1. Clone the repository:
   ```
   git clone https://github.com/yourusername/turnbase.git
   cd turnbase
   ```

2. Run database migrations:
   ```
   dotnet ef database update
   ```

3. Start the server:
   ```
   dotnet run --project Turnbase.Server
   ```

### API Endpoints

- `POST /api/games`: Create a new game
- `GET /api/games/{id}`: Retrieve game state
- `POST /api/games/{id}/moves`: Submit a move (authenticated)
- `GET /api/games/{id}/verify`: Verify the fairness of a game

## 💻 Usage Example

### Creating a Game
