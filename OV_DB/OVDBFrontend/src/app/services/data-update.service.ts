import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class DataUpdateService {
  public updateRequested$ = new BehaviorSubject<boolean>(true);
  constructor() { }

  requestUpdate() {
    this.updateRequested$.next(true);
  }


}
