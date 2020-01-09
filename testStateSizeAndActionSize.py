import os
import sys
import clr
import zlib
sys.path.append(str(os.getcwd())+"\\SabberStonePythonPort\\bin\\Release\\")
clr.AddReference("SabberStonePythonPort")
from SabberStonePythonPortNS import SabberStonePythonPort
game=SabberStonePythonPort()
maxActionSize=0
maxStateSize=0
lastBiggestState=0
lastBiggestAction=0
for i in range(0,1000):
    print(i)
    try:
        game.Reset()
        while not game.GetDone():
            nowState=list(game.GetPlayerViewGameBinary())
            compressed_data = zlib.compress(bytearray(nowState), level=9)
            nowStateSize=len(compressed_data)
            nowActionSize=game.GetNumActions()
            if nowStateSize>maxStateSize:
                print('max state now '+str(nowStateSize))
                lastBiggestState=maxStateSize
                maxStateSize=nowStateSize
            if nowActionSize>maxActionSize:
                print('max action size now '+str(nowActionSize))
                lastBiggestAction=maxActionSize
                maxActionSize=nowActionSize
            game.Step(game.GetNumActions()-1)
    except:
        print('error')

print('Max State: '+str(maxStateSize))
print('Max Action: '+str(maxActionSize))
print('2nd Max State: '+str(lastBiggestState))
print('2nd Max Action: '+str(lastBiggestAction))