import React, { useState } from 'react';
import { useAuthStore } from '@app/stores/auth.store';
import { useDevToolsStore } from '@app/stores/devTools.store';
import { useI18nStore } from '@app/stores/i18n.store';
import { useNotificationStore } from '@app/stores/notification.store';
import { useI18n } from '@app/i18n/use-i18n';
import { SYSTEM_INFO } from '@domain/identity/constants/system-info.constants';
import { M3Card } from '@shared/components/M3Card';
import { M3Button } from '@shared/components/M3Button';
import { KeyValueRow } from '@shared/components/KeyValueRow';
import { CodeBadge } from '@shared/components/CodeBadge';
import { StatusBadge } from '@shared/components/StatusBadge';
import {
  User,
  ShieldCheck,
  Building2,
  Globe,
  Clock,
  Terminal,
  Activity,
  CheckCircle,
  ChevronDown,
  ChevronRight,
  Copy,
  Check,
  Database,
  Cpu,
  Layers,
  Flag,
  FileText,
  Key,
} from 'lucide-react';
import { NOTIFICATION_ICONS, NOTIFICATION_ICON_CLASSES } from '@shared/theme/notification-theme';
import type { NotificationType } from '@shared/theme/notification-theme';

function CopyButton({ value, label = 'Copy' }: { value: string; label?: string }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    await navigator.clipboard.writeText(value);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <button
      onClick={handleCopy}
      className="flex items-center gap-1 px-2 py-1 rounded text-[10px] font-medium bg-m3-surface-container/50 hover:bg-m3-primary/10 text-m3-secondary hover:text-m3-primary transition-colors"
      title={`Copy ${label}`}
    >
      {copied ? <Check className="w-3 h-3 text-emerald-500" /> : <Copy className="w-3 h-3" />}
      {copied ? 'Copied' : label}
    </button>
  );
}

function StatCard({
  icon,
  label,
  value,
  subValue,
}: {
  icon: React.ReactNode;
  label: string;
  value: string | number;
  subValue?: string;
}) {
  return (
    <div className="p-3 bg-m3-surface-container/60 rounded-xl border border-m3-outline/25 flex items-center gap-3">
      <div className="text-m3-primary">{icon}</div>
      <div className="flex-1 min-w-0">
        <p className="text-[10px] text-m3-secondary">{label}</p>
        <p className="text-sm font-bold text-m3-on-surface">{value}</p>
        {subValue && <p className="text-[9px] text-m3-secondary/70 truncate">{subValue}</p>}
      </div>
    </div>
  );
}

function ExpandableSection({
  title,
  icon,
  children,
}: {
  title: string;
  icon: React.ReactNode;
  children: React.ReactNode;
}) {
  const [isExpanded, setIsExpanded] = useState(false);

  return (
    <div className="mt-4">
      <button
        onClick={() => setIsExpanded(!isExpanded)}
        className="w-full flex items-center justify-between px-3 py-2 rounded-xl hover:bg-m3-primary/5 transition-colors text-left"
      >
        <div className="flex items-center gap-2">
          <span className="text-m3-primary">{icon}</span>
          <span className="text-xs font-bold text-m3-on-surface">{title}</span>
        </div>
        {isExpanded ? (
          <ChevronDown className="w-4 h-4 text-m3-secondary" />
        ) : (
          <ChevronRight className="w-4 h-4 text-m3-secondary" />
        )}
      </button>
      {isExpanded && <div className="mt-2 px-2">{children}</div>}
    </div>
  );
}

export default function ProfileScreen(): React.JSX.Element {
  const { user, isAuthenticated } = useAuthStore();
  const { devUserId } = useDevToolsStore();
  const { language } = useI18nStore();
  const { notifications, addNotification } = useNotificationStore();
  const t = useI18n();

  const handleTriggerTest = (type: 'success' | 'warning' | 'error' | 'info') => {
    const titles = {
      success: t.devTestSuccess,
      warning: t.devTestWarning,
      error: t.devTestError,
      info: t.devTestInfo,
    };
    addNotification({
      title: titles[type],
      message: t.devTestMsg(new Date().toLocaleTimeString()),
      type,
    });
  };

  const activeUser =
    isAuthenticated && user
      ? user
      : {
          username: 'anonymous_developer',
          email: 'dev@logistica.pe',
          role: 'administrator (unauthenticated)',
          tenantId: '',
          tenantCode: '',
          tenantName: '',
          sessionTrackingId: '',
          permissions: [],
        };

  const sessionStart = localStorage.getItem('ums_session_start');
  const sessionStartDate = sessionStart ? new Date(parseInt(sessionStart, 10)) : null;

  return (
    <div className="space-y-6 select-none animate-fadeIn">
      <div>
        <h2 className="text-xl font-extrabold tracking-tight text-m3-on-surface">
          {t.profileTitle}
        </h2>
        <p className="text-xs text-m3-secondary">{t.profileSubtitle}</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-12 gap-6">
        {/* LEFT CARD: Connected User Session Info */}
        <M3Card
          variant="elevated"
          className="md:col-span-7 p-6 border border-m3-outline/25 bg-m3-surface-container/20"
        >
          <div className="flex items-center gap-4 mb-6">
            <div className="w-14 h-14 rounded-3xl bg-gradient-to-tr from-m3-primary to-indigo-600 flex items-center justify-center font-black text-lg text-white shadow-lg shadow-m3-primary/20">
              {activeUser.username.substring(0, 2).toUpperCase()}
            </div>
            <div>
              <h3 className="text-base font-extrabold text-m3-on-surface">{activeUser.username}</h3>
              <p className="text-[11px] text-m3-secondary">{activeUser.email}</p>
              {activeUser.tenantName && (
                <div className="flex items-center gap-1 mt-1">
                  <Building2 className="w-3 h-3 text-m3-secondary" />
                  <span className="text-[10px] text-m3-secondary">{activeUser.tenantName}</span>
                  <CodeBadge code={activeUser.tenantCode} />
                </div>
              )}
            </div>
          </div>

          <div className="space-y-3.5 text-xs">
            <KeyValueRow
              icon={<User className="w-3.5 h-3.5" />}
              label="User ID"
              value={
                <div className="flex items-center gap-2">
                  <span
                    className="font-mono text-m3-primary font-bold text-[11px] bg-m3-surface-container px-2 py-1 rounded border border-m3-outline/30 cursor-help select-all"
                    title={devUserId}
                  >
                    {devUserId.substring(0, 8)}…
                  </span>
                  <CopyButton value={devUserId} label="Copy" />
                </div>
              }
            />
            <KeyValueRow
              icon={<ShieldCheck className="w-3.5 h-3.5" />}
              label="Role"
              value={<CodeBadge code={activeUser.role} />}
            />
            <KeyValueRow
              icon={<Building2 className="w-3.5 h-3.5" />}
              label="Tenant"
              value={
                activeUser.tenantName ? (
                  <span className="font-medium text-m3-on-surface">{activeUser.tenantName}</span>
                ) : (
                  <span className="text-m3-secondary">No tenant selected</span>
                )
              }
            />
            <KeyValueRow
              icon={<Globe className="w-3.5 h-3.5" />}
              label="Language"
              value={
                <span className="font-extrabold uppercase text-m3-on-surface bg-m3-surface-container px-2 py-0.5 rounded border border-m3-outline/30">
                  {language}
                </span>
              }
            />
            <KeyValueRow
              icon={<Clock className="w-3.5 h-3.5" />}
              label="Session Start"
              value={
                sessionStartDate ? (
                  <span className="font-medium text-m3-on-surface">
                    {sessionStartDate.toLocaleString()}
                  </span>
                ) : (
                  <span className="text-m3-secondary">N/A</span>
                )
              }
            />
            <KeyValueRow
              icon={<Activity className="w-3.5 h-3.5" />}
              label="Session Status"
              value={
                <span className="text-emerald-500 font-extrabold flex items-center gap-1">
                  <CheckCircle className="w-3.5 h-3.5" /> {t.synced}
                </span>
              }
              bordered={false}
            />
          </div>

          {/* Session Tracking ID */}
          {activeUser.sessionTrackingId && (
            <ExpandableSection title="Session Tracking" icon={<Key className="w-4 h-4" />}>
              <div className="p-3 bg-m3-surface-container/40 rounded-xl">
                <div className="flex items-center justify-between">
                  <span className="text-[10px] text-m3-secondary">Session Tracking ID</span>
                  <CopyButton value={activeUser.sessionTrackingId} label="Copy" />
                </div>
                <p className="font-mono text-[10px] text-m3-primary mt-1 break-all">
                  {activeUser.sessionTrackingId}
                </p>
              </div>
            </ExpandableSection>
          )}

          <div className="mt-6 pt-5 border-t border-m3-outline/20">
            <h4 className="text-[10px] font-extrabold uppercase tracking-wider text-m3-secondary mb-3">
              {t.diagnosticTitle}
            </h4>
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
              <M3Button
                variant="tonal"
                onClick={() => handleTriggerTest('success')}
                className="py-1 px-2 text-[10px] h-8"
              >
                {t.active}
              </M3Button>
              <M3Button
                variant="outlined"
                onClick={() => handleTriggerTest('info')}
                className="py-1 px-2 text-[10px] h-8"
              >
                Info
              </M3Button>
              <M3Button
                variant="outlined"
                onClick={() => handleTriggerTest('warning')}
                className="py-1 px-2 text-[10px] h-8 text-amber-500 border-amber-500/30 hover:bg-amber-500/10"
              >
                Warning
              </M3Button>
              <M3Button
                variant="outlined"
                onClick={() => handleTriggerTest('error')}
                className="py-1 px-2 text-[10px] h-8 text-rose-500 border-rose-500/30 hover:bg-rose-500/10"
              >
                Error
              </M3Button>
            </div>
          </div>
        </M3Card>

        {/* RIGHT CARD: System Statistics */}
        <div className="md:col-span-5 flex flex-col gap-6">
          {/* Access Summary */}
          <M3Card variant="filled" className="p-5 rounded-2xl bg-m3-surface-container/40">
            <div className="flex items-center gap-2 mb-4">
              <ShieldCheck className="w-4 h-4 text-m3-primary" />
              <h3 className="text-xs font-bold uppercase tracking-widest text-m3-on-surface">
                Access Summary
              </h3>
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <StatCard
                icon={<Layers className="w-4 h-4" />}
                label="Modules"
                value="8"
                subValue="Identity, Authorization, etc."
              />
              <StatCard
                icon={<FileText className="w-4 h-4" />}
                label="Profiles"
                value="4"
                subValue="Admin, Operator, etc."
              />
              <StatCard
                icon={<Database className="w-4 h-4" />}
                label="Domain Resources"
                value="24"
                subValue="Aggregates & Entities"
              />
              <StatCard
                icon={<Flag className="w-4 h-4" />}
                label="Feature Flags"
                value="3"
                subValue="Active configurations"
              />
            </div>
          </M3Card>

          {/* Workspace Health */}
          <M3Card variant="filled" className="p-5 rounded-2xl bg-m3-surface-container/40">
            <div className="flex items-center gap-2 mb-4">
              <Activity className="w-4 h-4 text-m3-primary" />
              <h3 className="text-xs font-bold uppercase tracking-widest text-m3-on-surface">
                {t.workspaceHealth}
              </h3>
            </div>
            <div className="space-y-3 text-[11px] text-m3-secondary font-medium">
              <div className="p-3 bg-m3-surface-container/60 rounded-xl border border-m3-outline/25 flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Cpu className="w-4 h-4 text-m3-primary" />
                  <span>API Port</span>
                </div>
                <span className="font-mono text-m3-primary font-bold">{SYSTEM_INFO.apiPort}</span>
              </div>
              <div className="p-3 bg-m3-surface-container/60 rounded-xl border border-m3-outline/25 flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Database className="w-4 h-4 text-m3-primary" />
                  <span>Database</span>
                </div>
                <span className="text-emerald-500 font-extrabold flex items-center gap-1">
                  <CheckCircle className="w-3 h-3" /> {SYSTEM_INFO.dbEngine}
                </span>
              </div>
              <div className="p-3 bg-m3-surface-container/60 rounded-xl border border-m3-outline/25 flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Layers className="w-4 h-4 text-m3-primary" />
                  <span>Architecture</span>
                </div>
                <span className="font-extrabold text-m3-on-surface">
                  {SYSTEM_INFO.architecture}
                </span>
              </div>
            </div>
          </M3Card>

          {/* Audit Trail */}
          <M3Card variant="outlined" className="p-5 rounded-2xl border-m3-outline/40">
            <div className="flex items-center gap-2 mb-3">
              <Terminal className="w-4 h-4 text-indigo-400" />
              <h3 className="text-xs font-bold uppercase tracking-widest text-m3-on-surface">
                {t.auditTrail}
              </h3>
            </div>

            <div className="space-y-2.5 max-h-[140px] overflow-y-auto pr-1">
              {notifications.slice(0, 3).map(n => (
                <div
                  key={n.id}
                  className="p-2.5 bg-m3-surface-container/30 rounded-xl border border-m3-outline/20 text-[10px] leading-relaxed flex gap-2"
                >
                  <span className="flex-shrink-0 mt-0.5">
                    {React.createElement(NOTIFICATION_ICONS[n.type as NotificationType], {
                      className: `w-3.5 h-3.5 ${NOTIFICATION_ICON_CLASSES[n.type as NotificationType]}`,
                    })}
                  </span>
                  <div>
                    <p className="font-bold text-m3-on-surface">{n.title}</p>
                    <p className="text-m3-secondary">{n.message}</p>
                  </div>
                </div>
              ))}
              {notifications.length === 0 && (
                <p className="text-[10px] text-m3-secondary text-center py-4">{t.noAudit}</p>
              )}
            </div>
          </M3Card>

          {/* Technical Details - Expandable */}
          <M3Card variant="outlined" className="p-5 rounded-2xl border-m3-outline/40">
            <ExpandableSection title="Technical Details" icon={<Terminal className="w-4 h-4" />}>
              <div className="space-y-3">
                <div className="p-3 bg-m3-surface-container/40 rounded-xl">
                  <div className="flex items-center justify-between mb-2">
                    <span className="text-[10px] font-bold text-m3-secondary uppercase">
                      User Context
                    </span>
                    <CopyButton value={JSON.stringify(activeUser, null, 2)} label="Copy JSON" />
                  </div>
                  <pre className="text-[9px] font-mono text-m3-on-surface/70 bg-m3-surface-container/50 p-2 rounded overflow-x-auto">
                    {JSON.stringify(
                      {
                        userId: activeUser.id,
                        username: activeUser.username,
                        email: activeUser.email,
                        role: activeUser.role,
                        tenantId: activeUser.tenantId,
                        tenantCode: activeUser.tenantCode,
                        tenantName: activeUser.tenantName,
                        profileId: activeUser.profileId,
                        sessionTrackingId: activeUser.sessionTrackingId,
                      },
                      null,
                      2
                    )}
                  </pre>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 text-[10px]">
                  <div className="p-2 bg-m3-surface-container/30 rounded">
                    <span className="text-m3-secondary">User ID:</span>
                    <p className="font-mono text-m3-primary truncate">{activeUser.id}</p>
                  </div>
                  <div className="p-2 bg-m3-surface-container/30 rounded">
                    <span className="text-m3-secondary">Tenant ID:</span>
                    <p className="font-mono text-m3-primary truncate">
                      {activeUser.tenantId || 'N/A'}
                    </p>
                  </div>
                  <div className="p-2 bg-m3-surface-container/30 rounded">
                    <span className="text-m3-secondary">Profile ID:</span>
                    <p className="font-mono text-m3-primary truncate">
                      {activeUser.profileId || 'N/A'}
                    </p>
                  </div>
                  <div className="p-2 bg-m3-surface-container/30 rounded">
                    <span className="text-m3-secondary">Session ID:</span>
                    <p className="font-mono text-m3-primary truncate">
                      {activeUser.sessionTrackingId || 'N/A'}
                    </p>
                  </div>
                </div>
              </div>
            </ExpandableSection>
          </M3Card>
        </div>
      </div>
    </div>
  );
}
