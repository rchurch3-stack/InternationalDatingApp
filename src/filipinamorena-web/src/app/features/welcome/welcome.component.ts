import { Component } from '@angular/core';

@Component({
  selector: 'app-welcome',
  standalone: true,
  template: `<main class="container min-vh-100 d-flex align-items-center justify-content-center">
    <div class="card border-0 shadow p-5 text-center rounded-4">
      <i class="bi bi-heart-fill text-primary display-3"></i>
      <h1 class="fw-bold mt-3">Welcome to FilipinaMorena</h1>
      <p class="text-secondary mb-0">You're signed in. Your dating experience starts here.</p>
    </div>
  </main>`
})
export class WelcomeComponent {}
