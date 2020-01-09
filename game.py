import numpy as np
import logging
import os
import sys
import clr
import zlib
import hashlib

sys.path.append(str(os.getcwd())+"\\SabberStonePythonPort\\bin\\Release\\")
clr.AddReference("SabberStonePythonPort")
from SabberStonePythonPortNS import SabberStonePythonPort

class Game:

    def __init__(self):
        self.game=SabberStonePythonPort()
        self.game.Init()	
        if self.game.GetPlayerTurn() == 1:
            self.currentPlayer = 1
        else:
            self.currentPlayer = -1
        gameForState=self.game.GetDeepCopy()
        self.gameState = GameState(gameForState, self.currentPlayer)
        self.actionSpace = np.zeros(500)
        self.grid_shape = (1,200000)
        self.input_shape = (1,1,200000)
        self.name = 'hearthstone'
        self.state_size = 200000
        self.action_size = 500
    
    def identities(self, state, actionValues):
        return [(state,actionValues), (state,actionValues)]

    def reset(self):
        self.game.Reset()
        if self.game.GetPlayerTurn() == 1:
            self.currentPlayer = 1
        else:
            self.currentPlayer = -1
        gameForState=self.game.GetDeepCopy()
        self.gameState = GameState(gameForState, self.currentPlayer)
        return self.gameState

    def step(self, action):
        next_state, value, done = self.gameState.takeAction(action)
        self.gameState = next_state
        if self.game.GetPlayerTurn() == 1:
            self.currentPlayer = 1
        else:
            self.currentPlayer = -1
        info = None
        return ((next_state, value, done, info))


class GameState():
    def __init__(self, game, playerTurn):
        self.game=game
        self.playerTurn = playerTurn
        self.binary = self._binary()
        self.id = self._convertStateToId()
        self.allowedActions = self._allowedActions()
        self.isEndGame = self._checkForEndGame()
        self.value = self._getValue()
        self.score = self._getScore()

    def _allowedActions(self):
        allowed = []
        for i in range(self.game.GetNumActions()):
            allowed.append(i)
        return allowed

    def _binary(self):
        nowState=list(self.game.GetPlayerViewGameBinary())
        compressed_data = zlib.compress(bytearray(nowState), level=9)

        data=np.frombuffer(compressed_data, dtype=np.uint8)

        newPos=np.zeros(200000)
        if data.shape[0]<200000:
            newPos[:data.shape[0]]=data
        else:
            newPos[:200000]=data[:200000]

        position=np.array([newPos])

        return (position)

    def _convertStateToId(self):
        return hashlib.sha1(self.binary).hexdigest()

    def _checkForEndGame(self):
        self.game.GetDone()


    def _getValue(self):
        # This is the value of the state for the current player
        # i.e. if the previous player played a winning move, you lose
        val=self.game.GetValue()
        return (val, val, val)


    def _getScore(self):
        tmp = self.value
        return (tmp[1], tmp[2])

    def takeAction(self, action):
        newGame=self.game.GetDeepCopy()
        newGame.Step(action)
        if newGame.GetPlayerTurn() == 1:
            newPlayer = 1
        else:
            newPlayer = -1
        newState = GameState(newGame, newPlayer)
        value = 0
        done = 0

        if newState.isEndGame:
            value = newState.value[0]
            done = 1

        return (newState, value, done) 

    def render(self, logger):
        logger.info(' ')