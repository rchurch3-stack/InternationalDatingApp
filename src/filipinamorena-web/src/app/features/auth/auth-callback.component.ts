import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-auth-callback',
  standalone: true,
  template: `<main class="min-vh-100 d-flex justify-content-center align-items-center">
    <div class="text-center"><div class="spinner-border text-primary mb-3"></div><p>Finishing secure sign-in…</p></div>
  </main>`
})
export class AuthCallbackComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  ngOnInit(): void {
    void this.router.navigateByUrl(this.auth.completeExternalLogin() ? '/welcome' : '/login');
  }
}
