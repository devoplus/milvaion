import { useState, useRef, useEffect } from 'react'
import { formatDate } from '../utils/dateUtils'
import Icon from './Icon'
import './AuditInfoCard.css'

function AuditInfoContent({ auditInfo }) {
  return (
    <>
      <div className="audit-info-row">
        <span className="audit-info-label">
          <Icon name="person_add" size={16} />
          Created By
        </span>
        <span className="audit-info-value">
          {auditInfo.creatorUserName || '-'}
        </span>
      </div>
      <div className="audit-info-row">
        <span className="audit-info-label">
          <Icon name="calendar_today" size={16} />
          Created At
        </span>
        <span className="audit-info-value">
          {formatDate(auditInfo.creationDate)}
        </span>
      </div>
      <div className="audit-info-row">
        <span className="audit-info-label">
          <Icon name="edit" size={16} />
          Last Modified By
        </span>
        <span className="audit-info-value">
          {auditInfo.lastModifierUserName || '-'}
        </span>
      </div>
      <div className="audit-info-row">
        <span className="audit-info-label">
          <Icon name="update" size={16} />
          Last Modified At
        </span>
        <span className="audit-info-value">
          {formatDate(auditInfo.lastModificationDate)}
        </span>
      </div>
    </>
  )
}

function AuditInfoCard({ auditInfo, inline = false }) {
  const [isOpen, setIsOpen] = useState(false)
  const cardRef = useRef(null)

  useEffect(() => {
    if (inline) return

    const handleClickOutside = (event) => {
      if (cardRef.current && !cardRef.current.contains(event.target)) {
        setIsOpen(false)
      }
    }

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside)
    }

    return () => {
      document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [isOpen, inline])

  if (!auditInfo) return null

  if (inline) {
    return (
      <div className="audit-info-inline">
        <div className="audit-info-header">
          <Icon name="info" size={18} />
          <span>Audit Information</span>
        </div>
        <div className="audit-info-body">
          <AuditInfoContent auditInfo={auditInfo} />
        </div>
      </div>
    )
  }

  return (
    <div className="audit-info-wrapper" ref={cardRef}>
      <button
        className="audit-info-btn"
        onClick={() => setIsOpen(!isOpen)}
        title="Audit Information"
      >
        <Icon name="info" size={20} />
      </button>

      {isOpen && (
        <div className="audit-info-popover">
          <div className="audit-info-header">
            <Icon name="info" size={18} />
            <span>Audit Information</span>
          </div>
          <div className="audit-info-body">
            <AuditInfoContent auditInfo={auditInfo} />
          </div>
        </div>
      )}
    </div>
  )
}

export default AuditInfoCard
