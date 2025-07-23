import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private darkModeSubject = new BehaviorSubject<boolean>(false);
  public darkMode$ = this.darkModeSubject.asObservable();

  constructor() {
    // Check for saved theme preference or default to light mode
    const savedTheme = localStorage.getItem('theme');
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    
    const isDark = savedTheme === 'dark' || (savedTheme === null && prefersDark);
    this.setDarkMode(isDark);
  }

  toggleDarkMode(): void {
    this.setDarkMode(!this.darkModeSubject.value);
  }

  setDarkMode(isDark: boolean): void {
    this.darkModeSubject.next(isDark);
    
    // Apply theme to document root and body
    const htmlElement = document.documentElement;
    const bodyElement = document.body;
    
    if (isDark) {
      htmlElement.classList.add('dark-theme');
      bodyElement.classList.add('dark-theme');
    } else {
      htmlElement.classList.remove('dark-theme');
      bodyElement.classList.remove('dark-theme');
    }
    
    // Save preference
    localStorage.setItem('theme', isDark ? 'dark' : 'light');
  }

  get isDarkMode(): boolean {
    return this.darkModeSubject.value;
  }
}