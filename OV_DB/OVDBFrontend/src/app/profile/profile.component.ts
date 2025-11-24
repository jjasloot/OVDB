import { Component, OnInit, inject } from '@angular/core';
import { UntypedFormBuilder, UntypedFormGroup, Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ApiService } from '../services/api.service';
import { UserProfile, UpdateProfile, ChangePassword } from '../models/user-profile.model';
import { TrawellingConnectionStatus } from '../models/traewelling.model';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { TranslationService } from '../services/translation.service';
import { UserPreferenceService } from '../services/user-preference.service';
import { MatFormField, MatLabel, MatError } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatButton } from '@angular/material/button';
import { MatSelect, MatOption } from '@angular/material/select';
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle } from '@angular/material/card';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatIcon } from '@angular/material/icon';
import { MatList, MatListItem, MatListItemTitle, MatListItemLine } from '@angular/material/list';
import { MatDivider } from '@angular/material/divider';
import { Router } from '@angular/router';
import { AuthenticationService } from '../services/authentication.service';

@Component({
  selector: 'app-profile',
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.scss'],
  imports: [
    FormsModule, 
    ReactiveFormsModule, 
    MatFormField, 
    MatLabel, 
    MatError,
    MatInput, 
    MatButton, 
    MatSelect, 
    MatOption, 
    MatCard, 
    MatCardContent, 
    MatCardHeader, 
    MatCardTitle, 
    MatProgressSpinner, 
    MatIcon,
    MatList,
    MatListItem,
    MatListItemTitle,
    MatListItemLine,
    MatDivider,
    TranslateModule
  ]
})
export class ProfileComponent implements OnInit {
  private formBuilder = inject(UntypedFormBuilder);
  private apiService = inject(ApiService);
  private snackBar = inject(MatSnackBar);
  private translateService = inject(TranslateService);
  private translationService = inject(TranslationService);
  private userPreferenceService = inject(UserPreferenceService);
  private router = inject(Router);
  private authService = inject(AuthenticationService);

  profileForm: UntypedFormGroup;
  passwordForm: UntypedFormGroup;
  userProfile: UserProfile | null = null;
  trawellingStatus: TrawellingConnectionStatus | null = null;
  loading = false;
  savingProfile = false;
  changingPassword = false;
  trawellingLoading = false;
  trawellingConnecting = false;
  trawellingDisconnecting = false;
  sessions: any[] = [];
  sessionsLoading = false;
  revokingSessionId: number | null = null;

  languages = [
    { value: 'en', label: 'English' },
    { value: 'nl', label: 'Nederlands' }
  ];

  constructor() {
    this.profileForm = this.formBuilder.group({
      preferredLanguage: ['', Validators.required],
      telegramUserId: [null]
    });

    this.passwordForm = this.formBuilder.group({
      currentPassword: ['', Validators.required],
      newPassword: ['', [Validators.required, Validators.minLength(10)]],
      confirmPassword: ['', Validators.required]
    }, {
      validators: this.passwordMatchValidator
    });
  }

  ngOnInit(): void {
    this.loadProfile();
    this.loadTrawellingStatus();
    this.loadSessions();
  }

  passwordMatchValidator(group: UntypedFormGroup) {
    const newPassword = group.get('newPassword')?.value;
    const confirmPassword = group.get('confirmPassword')?.value;
    return newPassword === confirmPassword ? null : { passwordMismatch: true };
  }

  loadProfile(): void {
    this.loading = true;
    this.apiService.getUserProfile().subscribe({
      next: (profile) => {
        this.userProfile = profile;
        this.profileForm.patchValue({
          preferredLanguage: profile.preferredLanguage || '',
          telegramUserId: profile.telegramUserId || null
        });
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading profile:', error);
        this.showMessage('PROFILE.ERROR_LOADING');
        this.loading = false;
      }
    });
  }

  saveProfile(): void {
    if (this.profileForm.valid) {
      this.savingProfile = true;
      const updateProfile: UpdateProfile = {
        preferredLanguage: this.profileForm.value.preferredLanguage,
        telegramUserId: this.profileForm.value.telegramUserId
      };

      this.apiService.updateUserProfile(updateProfile).subscribe({
        next: () => {
          this.showMessage('PROFILE.SAVED_SUCCESSFULLY');
          this.savingProfile = false;
          
          // Update language if changed
          if (updateProfile.preferredLanguage !== this.translationService.language) {
            this.translationService.language = updateProfile.preferredLanguage as 'nl' | 'en';
          }
        },
        error: (error) => {
          console.error('Error saving profile:', error);
          this.showMessage('PROFILE.ERROR_SAVING');
          this.savingProfile = false;
        }
      });
    }
  }

  changePassword(): void {
    if (this.passwordForm.valid) {
      this.changingPassword = true;
      const changePassword: ChangePassword = {
        currentPassword: this.passwordForm.value.currentPassword,
        newPassword: this.passwordForm.value.newPassword
      };

      this.apiService.changePassword(changePassword).subscribe({
        next: () => {
          this.showMessage('PROFILE.PASSWORD_CHANGED_SUCCESSFULLY');
          this.passwordForm.reset();
          this.changingPassword = false;
        },
        error: (error) => {
          console.error('Error changing password:', error);
          const errorMessage = error.error?.message || error.error || 'PROFILE.ERROR_CHANGING_PASSWORD';
          this.showMessage(errorMessage);
          this.changingPassword = false;
        }
      });
    }
  }

  private showMessage(messageKey: string): void {
    this.translateService.get(messageKey).subscribe(message => {
      this.snackBar.open(message, '', { duration: 3000 });
    });
  }

  loadTrawellingStatus(): void {
    this.trawellingLoading = true;
    this.apiService.getTrawellingStatus().subscribe({
      next: (status) => {
        this.trawellingStatus = status;
        this.trawellingLoading = false;
      },
      error: (error) => {
        console.error('Error loading Träwelling status:', error);
        this.trawellingLoading = false;
      }
    });
  }

  connectTraewelling(): void {
    this.trawellingConnecting = true;
    this.apiService.getTrawellingConnectUrl().subscribe({
      next: (response) => {
        // Open the authorization URL in a new window
        const authWindow = window.open(response.authorizationUrl, '_blank', 'width=600,height=700');
        
        // Listen for messages from the popup
        const messageListener = (event: MessageEvent) => {
          if (event.origin !== window.location.origin) {
            return; // Ignore messages from other origins
          }
          
          if (event.data.type === 'oauth-success') {
            window.removeEventListener('message', messageListener);
            this.trawellingConnecting = false;
            this.showMessage('PROFILE.TRAEWELLING_CONNECTED_SUCCESSFULLY');
            this.loadTrawellingStatus();
          } else if (event.data.type === 'oauth-error') {
            window.removeEventListener('message', messageListener);
            this.trawellingConnecting = false;
            this.showMessage('PROFILE.TRAEWELLING_ERROR_CONNECTING');
          }
        };
        
        window.addEventListener('message', messageListener);
        
        // Fallback: Poll for the window to be closed (in case postMessage fails)
        const checkClosed = setInterval(() => {
          if (authWindow?.closed) {
            clearInterval(checkClosed);
            window.removeEventListener('message', messageListener);
            if (this.trawellingConnecting) {
              // Only reload if we haven't already handled the message
              this.trawellingConnecting = false;
              this.loadTrawellingStatus();
            }
          }
        }, 1000);
      },
      error: (error) => {
        console.error('Error getting Träwelling connect URL:', error);
        this.showMessage('PROFILE.TRAEWELLING_ERROR_CONNECTING');
        this.trawellingConnecting = false;
      }
    });
  }

  disconnectTraewelling(): void {
    this.trawellingDisconnecting = true;
    this.apiService.disconnectTraewelling().subscribe({
      next: () => {
        this.showMessage('PROFILE.TRAEWELLING_DISCONNECTED_SUCCESSFULLY');
        this.trawellingStatus = { connected: false };
        this.trawellingDisconnecting = false;
      },
      error: (error) => {
        console.error('Error disconnecting Träwelling:', error);
        this.showMessage('PROFILE.TRAEWELLING_ERROR_DISCONNECTING');
        this.trawellingDisconnecting = false;
      }
    });
  }

  viewTrawellingTrips(): void {
    this.router.navigate(['/admin/traewelling']);
  }

  loadSessions(): void {
    this.sessionsLoading = true;
    this.authService.getActiveSessions().subscribe({
      next: (sessions) => {
        this.sessions = sessions;
        this.sessionsLoading = false;
      },
      error: (error) => {
        console.error('Error loading sessions:', error);
        this.showMessage('PROFILE.ERROR_LOADING_SESSIONS');
        this.sessionsLoading = false;
      }
    });
  }

  revokeSession(sessionId: number): void {
    if (confirm(this.translateService.instant('PROFILE.CONFIRM_REVOKE_SESSION'))) {
      this.revokingSessionId = sessionId;
      this.authService.revokeSession(sessionId).subscribe({
        next: () => {
          this.showMessage('PROFILE.SESSION_REVOKED_SUCCESSFULLY');
          this.revokingSessionId = null;
          this.loadSessions(); // Reload the list
        },
        error: (error) => {
          console.error('Error revoking session:', error);
          this.showMessage('PROFILE.ERROR_REVOKING_SESSION');
          this.revokingSessionId = null;
        }
      });
    }
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    return date.toLocaleString();
  }

  isCurrentSession(session: any): boolean {
    // Simple heuristic: the most recently used session is likely the current one
    if (!this.sessions.length) return false;
    const mostRecent = this.sessions.reduce((prev, current) => {
      const prevTime = new Date(prev.lastUsedAt || prev.createdAt).getTime();
      const currentTime = new Date(current.lastUsedAt || current.createdAt).getTime();
      return currentTime > prevTime ? current : prev;
    });
    return session.id === mostRecent.id;
  }

  parseUserAgent(userAgent: string): string {
    if (!userAgent || userAgent === 'Unknown Device') {
      return 'Unknown Device';
    }

    // Parse browser
    let browser = 'Unknown Browser';
    let browserVersion = '';
    
    if (userAgent.includes('Firefox/')) {
      const match = userAgent.match(/Firefox\/([\d.]+)/);
      browser = 'Firefox';
      browserVersion = match ? match[1] : '';
    } else if (userAgent.includes('Edg/')) {
      const match = userAgent.match(/Edg\/([\d.]+)/);
      browser = 'Edge';
      browserVersion = match ? match[1] : '';
    } else if (userAgent.includes('Chrome/')) {
      const match = userAgent.match(/Chrome\/([\d.]+)/);
      browser = 'Chrome';
      browserVersion = match ? match[1] : '';
    } else if (userAgent.includes('Safari/') && !userAgent.includes('Chrome')) {
      const match = userAgent.match(/Version\/([\d.]+)/);
      browser = 'Safari';
      browserVersion = match ? match[1] : '';
    }

    // Parse OS
    let os = '';
    if (userAgent.includes('Windows NT 10.0')) {
      os = 'Windows 10/11';
    } else if (userAgent.includes('Windows NT 6.3')) {
      os = 'Windows 8.1';
    } else if (userAgent.includes('Windows NT 6.2')) {
      os = 'Windows 8';
    } else if (userAgent.includes('Windows NT 6.1')) {
      os = 'Windows 7';
    } else if (userAgent.includes('Windows')) {
      os = 'Windows';
    } else if (userAgent.includes('Mac OS X')) {
      const match = userAgent.match(/Mac OS X ([\d_]+)/);
      os = match ? `macOS ${match[1].replace(/_/g, '.')}` : 'macOS';
    } else if (userAgent.includes('Android')) {
      const match = userAgent.match(/Android ([\d.]+)/);
      os = match ? `Android ${match[1]}` : 'Android';
    } else if (userAgent.includes('iPhone') || userAgent.includes('iPad')) {
      const device = userAgent.includes('iPad') ? 'iPad' : 'iPhone';
      const match = userAgent.match(/OS ([\d_]+)/);
      const version = match ? match[1].replace(/_/g, '.') : '';
      os = version ? `${device} (iOS ${version})` : device;
    } else if (userAgent.includes('Linux')) {
      os = 'Linux';
    }

    // Format the result
    const versionStr = browserVersion ? ` ${browserVersion.split('.')[0]}` : '';
    const result = os ? `${browser}${versionStr} on ${os}` : `${browser}${versionStr}`;
    
    return result;
  }
}