n=6
max = 10000
C = [[max for x in range(n)] for x in range(n)]
C[0][1] = 4
C[0][2] = 1
C[0][3] = 5
C[0][4] = 8
C[0][5] = 10
C[2][1] = 2
C[3][4] = 2
C[4][5] = 1
"""
C[0][1] = 10
C[0][3] = 30
C[0][4] = 100
C[1][2] = 50
C[2][4] = 10
C[3][2] = 20
C[3][4] = 60
"""

for i in range(n):
    C[i][i]=0

P = [[0 for x in range(n)] for x in range(n)]

print ("Initial Matrix C:")
for row in C:
	print(row)
	
def djikstra(C):
    V = set()
    
    S = set([0])
    n = len(C)
    for i in range(n):
        V.add(i)
    
    # Initialize D and P
    P = [0 for x in range(n)]
    D = [0 for x in range(n)]
    for i in range(1,n):
        D[i] = C[0][i]

    for i in range(n-1):
        
        
        # Choose a vertex w in V-S such that D[w] is minimum
        w = None
        for vertex in V-S:
            if w is None:
                w=vertex
            elif D[vertex]<D[w]:
                w=vertex
    
        S.add(w)

        # Update D and P
        for v in V-S:
            if (D[v] > D[w] + C[w][v]):
                D[v] = D[w] + C[w][v]
                P[v] = w

        print('\n\n')
        print('D =', D)
        print('P =', P)

djikstra(C)
