import { useMemo } from 'react'
import Icon from './Icon'
import './WorkflowDAG.css'

const stepStatusIcons = {
  0: 'hourglass_empty',  // Pending
  1: 'sync',             // Running
  2: 'check_circle',     // Completed
  3: 'error',            // Failed
  4: 'skip_next',        // Skipped
  5: 'cancel',           // Cancelled
  6: 'schedule',         // Delayed
}

const stepStatusColors = {
  0: '#94a3b8',  // Pending - gray
  1: '#3b82f6',  // Running - blue
  2: '#22c55e',  // Completed - green
  3: '#ef4444',  // Failed - red
  4: '#a855f7',  // Skipped - purple
  5: '#6b7280',  // Cancelled - dark gray
  6: '#f59e0b',  // Delayed - amber
}

const stepStatusLabels = {
  0: 'Pending',
  1: 'Running',
  2: 'Completed',
  3: 'Failed',
  4: 'Skipped',
  5: 'Cancelled',
  6: 'Delayed',
}

/**
 * DAG visualization component for workflow steps.
 * Renders steps as nodes and edges as SVG arrows.
 */
function WorkflowDAG({ steps = [], stepRuns = null, onStepClick = null }) {
  // Build layout using topological sort with levels
  const { nodes, edges, width, height } = useMemo(() => {
    if (steps.length === 0) return { nodes: [], edges: [], width: 400, height: 200 }

    // Build adjacency
    const stepMap = new Map(steps.map(s => [s.id, s]))
    const inDeps = new Map()
    const outEdges = new Map()

    steps.forEach(s => {
      inDeps.set(s.id, [])
      outEdges.set(s.id, [])
    })

    steps.forEach(s => {
      if (s.dependsOnStepIds) {
        const deps = s.dependsOnStepIds.split(',').map(d => d.trim()).filter(Boolean)
        deps.forEach(depId => {
          if (stepMap.has(depId)) {
            inDeps.get(s.id).push(depId)
            outEdges.get(depId).push(s.id)
          }
        })
      }
    })

    // Topological sort to determine levels
    const levels = new Map()
    const queue = []
    const inDegree = new Map()

    steps.forEach(s => {
      inDegree.set(s.id, inDeps.get(s.id).length)
      if (inDeps.get(s.id).length === 0) {
        queue.push(s.id)
        levels.set(s.id, 0)
      }
    })

    let i = 0
    while (i < queue.length) {
      const nodeId = queue[i++]
      const level = levels.get(nodeId)
      for (const childId of (outEdges.get(nodeId) || [])) {
        const newDegree = inDegree.get(childId) - 1
        inDegree.set(childId, newDegree)
        const childLevel = Math.max(levels.get(childId) || 0, level + 1)
        levels.set(childId, childLevel)
        if (newDegree === 0) queue.push(childId)
      }
    }

    // Group by level
    const levelGroups = new Map()
    steps.forEach(s => {
      const level = levels.get(s.id) || 0
      if (!levelGroups.has(level)) levelGroups.set(level, [])
      levelGroups.get(level).push(s)
    })

    const nodeWidth = 200
    const nodeHeight = 70
    const hGap = 80
    const vGap = 40
    const paddingX = 40
    const paddingY = 40

    const maxLevel = Math.max(...levelGroups.keys(), 0)
    const maxNodesInLevel = Math.max(...[...levelGroups.values()].map(g => g.length), 1)

    const totalWidth = (maxLevel + 1) * (nodeWidth + hGap) + paddingX * 2
    const totalHeight = maxNodesInLevel * (nodeHeight + vGap) + paddingY * 2

    // Calculate positions
    const nodePositions = new Map()

    for (const [level, group] of levelGroups) {
      const x = paddingX + level * (nodeWidth + hGap)
      const groupHeight = group.length * (nodeHeight + vGap) - vGap
      const startY = paddingY + (totalHeight - paddingY * 2 - groupHeight) / 2

      group.forEach((step, idx) => {
        const y = startY + idx * (nodeHeight + vGap)
        // Use stored positions if available, otherwise use calculated
        const posX = step.positionX ?? x
        const posY = step.positionY ?? y
        nodePositions.set(step.id, { x: posX, y: posY })
      })
    }

    const positionedNodes = [...nodePositions.values()]
    const minX = positionedNodes.length ? Math.min(...positionedNodes.map(pos => pos.x)) : 0
    const minY = positionedNodes.length ? Math.min(...positionedNodes.map(pos => pos.y)) : 0
    const offsetX = paddingX - minX
    const offsetY = paddingY - minY

    nodePositions.forEach((pos, id) => {
      nodePositions.set(id, {
        x: pos.x + offsetX,
        y: pos.y + offsetY,
      })
    })

    const normalizedNodes = [...nodePositions.values()]
    const normalizedMaxX = normalizedNodes.length ? Math.max(...normalizedNodes.map(pos => pos.x + nodeWidth)) : totalWidth
    const normalizedMaxY = normalizedNodes.length ? Math.max(...normalizedNodes.map(pos => pos.y + nodeHeight)) : totalHeight

    // Build step run status map
    const stepRunMap = new Map()
    if (stepRuns) {
      stepRuns.forEach(sr => stepRunMap.set(sr.workflowStepId, sr))
    }

    // Create nodes
    const nodes = steps.map(step => {
      const pos = nodePositions.get(step.id) || { x: 0, y: 0 }
      const run = stepRunMap.get(step.id)
      return {
        id: step.id,
        label: step.stepName,
        jobName: step.jobDisplayName || '',
        x: pos.x,
        y: pos.y,
        width: nodeWidth,
        height: nodeHeight,
        status: run?.status ?? null,
        condition: step.condition,
        delay: step.delaySeconds,
        occurrenceId: run?.occurrenceId,
      }
    })

    // Create edges
    const edgeList = []
    steps.forEach(step => {
      if (step.dependsOnStepIds) {
        const deps = step.dependsOnStepIds.split(',').map(d => d.trim()).filter(Boolean)
        deps.forEach(depId => {
          if (nodePositions.has(depId) && nodePositions.has(step.id)) {
            const from = nodePositions.get(depId)
            const to = nodePositions.get(step.id)
            edgeList.push({
              fromId: depId,
              toId: step.id,
              x1: from.x + nodeWidth,
              y1: from.y + nodeHeight / 2,
              x2: to.x,
              y2: to.y + nodeHeight / 2,
            })
          }
        })
      }
    })

    return {
      nodes,
      edges: edgeList,
      width: Math.max(totalWidth, normalizedMaxX + paddingX),
      height: Math.max(totalHeight, normalizedMaxY + paddingY),
    }
  }, [steps, stepRuns])

  if (steps.length === 0) {
    return (
      <div className="dag-empty">
        <Icon name="schema" size={40} />
        <p>No steps defined</p>
      </div>
    )
  }

  return (
    <div className="dag-viewport">
      <svg width={width} height={height} className="dag-svg">
        <defs>
          <marker id="arrowhead" markerWidth="10" markerHeight="7" refX="10" refY="3.5" orient="auto">
            <polygon points="0 0, 10 3.5, 0 7" fill="var(--text-muted)" />
          </marker>
          <marker id="arrowhead-active" markerWidth="10" markerHeight="7" refX="10" refY="3.5" orient="auto">
            <polygon points="0 0, 10 3.5, 0 7" fill="#3b82f6" />
          </marker>
        </defs>

        {/* Edges */}
        {edges.map((edge, i) => {
          const midX = (edge.x1 + edge.x2) / 2
          return (
            <path
              key={`edge-${i}`}
              d={`M ${edge.x1} ${edge.y1} C ${midX} ${edge.y1}, ${midX} ${edge.y2}, ${edge.x2} ${edge.y2}`}
              fill="none"
              stroke="var(--border-color)"
              strokeWidth="2"
              markerEnd="url(#arrowhead)"
              className="dag-edge"
            />
          )
        })}

        {/* Nodes */}
        {nodes.map(node => {
          const statusColor = node.status !== null ? stepStatusColors[node.status] : '#94a3b8'
          const statusLabel = node.status !== null ? stepStatusLabels[node.status] : null

          return (
            <g
              key={node.id}
              className="dag-node"
              transform={`translate(${node.x}, ${node.y})`}
              onClick={() => onStepClick?.(node)}
              style={{ cursor: onStepClick ? 'pointer' : 'default' }}
            >
              <rect
                width={node.width}
                height={node.height}
                rx="10"
                ry="10"
                fill="var(--bg-secondary)"
                stroke={statusColor}
                strokeWidth={node.status === 1 ? 3 : 2}
                className={node.status === 1 ? 'dag-node-running' : ''}
              />

              {/* Step name */}
              <text
                x={node.width / 2}
                y={22}
                textAnchor="middle"
                fill="var(--text-primary)"
                fontSize="13"
                fontWeight="600"
              >
                {node.label.length > 22 ? node.label.substring(0, 20) + '...' : node.label}
              </text>

              {/* Job name */}
              <text
                x={node.width / 2}
                y={40}
                textAnchor="middle"
                fill="var(--text-muted)"
                fontSize="11"
              >
                {node.jobName.length > 26 ? node.jobName.substring(0, 24) + '...' : node.jobName}
              </text>

              {/* Status badge */}
              {statusLabel && (
                <g transform={`translate(${node.width / 2 - 30}, ${node.height - 22})`}>
                  <rect width="60" height="16" rx="8" fill={statusColor} opacity="0.15" />
                  <text x="30" y="12" textAnchor="middle" fill={statusColor} fontSize="10" fontWeight="600">
                    {statusLabel}
                  </text>
                </g>
              )}

              {/* Condition indicator */}
              {node.condition && (
                <g transform={`translate(${node.width - 22}, 4)`}>
                  <title>Has condition: {node.condition}</title>
                  <circle cx="8" cy="8" r="8" fill="#f59e0b" opacity="0.2" />
                  <text x="8" y="12" textAnchor="middle" fill="#d97706" fontSize="10">?</text>
                </g>
              )}

              {/* Delay indicator */}
              {node.delay > 0 && (
                <g transform={`translate(4, 4)`}>
                  <title>Has delay: {node.delay}s</title>
                  <circle cx="8" cy="8" r="8" fill="#8b5cf6" opacity="0.2" />
                  <text x="8" y="12" textAnchor="middle" fill="#7c3aed" fontSize="9">⏱</text>
                </g>
              )}
            </g>
          )
        })}
      </svg>
    </div>
  )
}

export default WorkflowDAG
