import React from 'react';
import { User, Mail, Shield, Hash, ToggleRight, Lock, Unlock, Pencil, Save, X } from 'lucide-react';
import { UserAccount } from '@domain/identity/models/user-account.model';
import { useInlineEdit } from '@app/hooks/use-inline-edit';
import { useResetOnChange } from '@app/hooks/use-reset-on-change';
import { useNotificationStore } from '@app/stores/notification.store';
import { USER_CATEGORIES } from '@domain/identity/constants/user-account.constants';
import { M3Card } from '@shared/components/M3Card';
import { M3Button } from '@shared/components/M3Button';
import { M3TextField } from '@shared/components/M3TextField';
import { M3Select } from '@shared/components/M3Select';
import { M3Dialog } from '@shared/components/M3Dialog';
import { StatusBadge } from '@shared/components/StatusBadge';
import { CodeBadge } from '@shared/components/CodeBadge';
import { KeyValueRow } from '@shared/components/KeyValueRow';
import { SectionHeader } from '@shared/components/SectionHeader';
import { IconButton } from '@shared/components/Tooltip';
import { useI18n } from '@app/i18n/use-i18n';
import { useStatusLabel } from '@app/hooks/use-status-label';

interface UserAccountProfileCardProps {
  account: UserAccount;
  onActivate: () => void;
  onBlock: () => void;
  onRestore: () => void;
  onAccountUpdate: (accountId: string, patch: Partial<UserAccount>) => void;
  onEditingChange?: (isEditing: boolean) => void;
}

interface UserAccountDraft {
  email: string;
  category: 'Internal' | 'External' | 'B2B' | 'Partner' | 'ServiceAccount';
}

export const UserAccountProfileCard: React.FC<UserAccountProfileCardProps> = ({
  account,
  onActivate,
  onBlock,
  onRestore,
  onAccountUpdate,
  onEditingChange,
}) => {
  const t = useI18n();
  const getStatusLabel = useStatusLabel();
  const addNotification = useNotificationStore(s => s.addNotification);

  const edit = useInlineEdit<UserAccountDraft>(['email', 'category']);
  const [showDiscardDialog, setShowDiscardDialog] = React.useState(false);

  // Reset editing state when the account changes
  useResetOnChange(account.userAccountId, () => {
    edit.cancelEdit();
    onEditingChange?.(false);
  });

  const openAccountEdit = () => {
    edit.openEdit(account.userAccountId, {
      email: account.email,
      category: account.category as 'Internal' | 'External' | 'B2B' | 'Partner' | 'ServiceAccount',
    });
    onEditingChange?.(true);
  };

  const cancelAccountEdit = () => {
    edit.cancelEdit();
    onEditingChange?.(false);
  };

  const saveAccountEdit = () => {
    const email = (edit.draft.email ?? '').trim().toLowerCase();
    const category = edit.draft.category;
    if (!email) return;

    // Email regex validation
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(email)) {
      addNotification({
        title: t.invalidEmail || 'Invalid Email',
        message: t.invalidEmailMsg || 'Please enter a valid email address.',
        type: 'error',
      });
      return;
    }

    const result = edit.commitEdit();
    if (!result) return;

    onAccountUpdate(account.userAccountId, {
      email,
      category: category as 'Internal' | 'External' | 'B2B' | 'Partner' | 'ServiceAccount',
    });

    addNotification({
      title: t.notifUserUpdated || 'User Updated',
      message: t.notifUserUpdatedMsg || `User account details successfully saved for ${email}.`,
      type: 'success',
    });
    onEditingChange?.(false);
  };

  const renderActions = () => {
    switch (account.status) {
      case 'Pending':
        return (
          <M3Button variant="filled" onClick={onActivate} className="text-[10px] h-7">
            <ToggleRight className="w-3.5 h-3.5 mr-1" /> {t.activateBtn}
          </M3Button>
        );
      case 'Active':
        return (
          <M3Button
            variant="outlined"
            onClick={onBlock}
            className="text-[10px] h-7 text-m3-error border-m3-error/30"
          >
            <Lock className="w-3.5 h-3.5 mr-1" /> {t.blockBtn}
          </M3Button>
        );
      case 'Blocked':
        return (
          <M3Button variant="filled" onClick={onRestore} className="text-[10px] h-7">
            <Unlock className="w-3.5 h-3.5 mr-1" /> {t.restoreBtn}
          </M3Button>
        );
      default:
        return null;
    }
  };

  return (
    <>
      {/* Discard-changes dialog */}
      <M3Dialog
        open={showDiscardDialog}
        title={t.unsavedChanges}
        message={t.unsavedChangesMsg}
        onScrimClick={() => setShowDiscardDialog(false)}
        actions={[
          {
            label: t.cancelEdit || 'Cancel',
            variant: 'outlined',
            onClick: () => setShowDiscardDialog(false),
          },
          {
            label: t.discardChanges || 'Discard',
            variant: 'filled',
            className: 'bg-m3-error hover:bg-m3-error/90 border-0',
            onClick: () => {
              setShowDiscardDialog(false);
              cancelAccountEdit();
            },
          },
        ]}
      />

      <M3Card
        variant="elevated"
        className="p-5 border border-m3-outline/25 bg-m3-surface-container/20 shadow-sm group"
        onDoubleClick={() => !edit.hasEditing && openAccountEdit()}
      >
        {!edit.hasEditing ? (
          <div className="space-y-4">
            <SectionHeader
              title={account.email}
              subtitle={account.category}
              actions={
                <div className="flex items-center gap-2">
                  {renderActions()}
                  <IconButton
                    tooltip={t.editBtn || 'Edit'}
                    onClick={openAccountEdit}
                    className="opacity-0 group-hover:opacity-100"
                  >
                    <Pencil className="w-3.5 h-3.5" />
                  </IconButton>
                </div>
              }
            />

            <div className="space-y-3 text-xs">
              <KeyValueRow
                icon={<Mail className="w-3.5 h-3.5" />}
                label={t.userEmail}
                value={
                  <span className="font-mono text-m3-on-surface text-xs">{account.email}</span>
                }
              />
              <KeyValueRow
                icon={<Shield className="w-3.5 h-3.5" />}
                label={t.userCategory}
                value={<CodeBadge code={account.category} />}
              />
              <KeyValueRow
                icon={<User className="w-3.5 h-3.5" />}
                label={t.status}
                value={
                  <StatusBadge status={account.status} label={getStatusLabel(account.status)} />
                }
              />
              {account.identityReference && (
                <KeyValueRow
                  icon={<Hash className="w-3.5 h-3.5" />}
                  label={t.identityReference}
                  value={
                    <span className="font-mono text-m3-primary text-xs bg-m3-surface-container px-2 py-0.5 rounded border border-m3-outline/30 font-semibold">
                      {account.identityReference}
                    </span>
                  }
                />
              )}
              {account.identityReferenceType && (
                <KeyValueRow
                  icon={<Hash className="w-3.5 h-3.5" />}
                  label={t.identityReferenceType}
                  value={<CodeBadge code={account.identityReferenceType} />}
                  bordered={false}
                />
              )}
            </div>
            <p className="text-xs text-m3-secondary/50 mt-3 text-center">{t.doubleClickToEdit}</p>
          </div>
        ) : (
          /* UserAccount inline-edit form */
          <div className="space-y-4 animate-fadeIn">
            <div className="flex items-center justify-between">
              <span className="text-sm font-semibold text-m3-primary flex items-center gap-1.5">
                <Pencil className="w-3.5 h-3.5" /> {t.editUserAccount || 'Edit User Account'}
              </span>
              <IconButton
                tooltip={t.cancelEdit || 'Cancel'}
                onClick={() => setShowDiscardDialog(true)}
              >
                <X className="w-3.5 h-3.5" />
              </IconButton>
            </div>

            <M3TextField
              label={t.userEmail}
              required
              value={edit.draft.email ?? ''}
              onChange={e => edit.setField('email', e.target.value)}
            />

            <M3Select
              label={t.userCategory}
              value={edit.draft.category ?? ''}
              onChange={e => edit.setField('category', e.target.value)}
            >
              {USER_CATEGORIES.map(c => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </M3Select>

            <div className="flex gap-3 pt-1">
              <M3Button
                variant="filled"
                onClick={saveAccountEdit}
                className="flex-1 flex items-center justify-center gap-1.5"
              >
                <Save className="w-3.5 h-3.5" /> {t.saveBtn || 'Save'}
              </M3Button>
              <M3Button
                variant="outlined"
                onClick={() => setShowDiscardDialog(true)}
                className="flex-1"
              >
                {t.cancelBtn || 'Cancel'}
              </M3Button>
            </div>
          </div>
        )}
      </M3Card>
    </>
  );
};
