import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

export interface AiSettingsDto {
  provider: string;
  hasClaudeApiKey: boolean;
  maskedClaudeApiKey?: string;
  hasGeminiApiKey: boolean;
  maskedGeminiApiKey?: string;
  model: string;
  totalInputTokens: number;
  totalOutputTokens: number;
  estimatedCostUsd: number;
}

export interface SaveAiSettingsRequest {
  provider?: string;
  claudeApiKey?: string;
  geminiApiKey?: string;
  model?: string;
}

export interface AiStreamChunk {
  type: 'text' | 'usage' | 'error';
  text?: string;
  inputTokens?: number;
  outputTokens?: number;
  errorMessage?: string;
}

export interface AiChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface AiContextResult {
  systemPrompt: string;
  userMessage: string;
  errorMessage?: string;
}

@Injectable({ providedIn: 'root' })
export class AiService {
  private SETTINGS_URL = `${environment.apiUrl}/ai-settings`;
  private AI_URL = `${environment.apiUrl}/ai`;
  private http = inject(HttpClient);
  private authService = inject(AuthService);

  private getHeaders(): HttpHeaders {
    const token = this.authService.getToken();
    return new HttpHeaders({
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`
    });
  }

  // --- Settings CRUD ---

  getSettings(): Observable<AiSettingsDto> {
    return this.http.get<AiSettingsDto>(this.SETTINGS_URL, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  saveSettings(data: SaveAiSettingsRequest): Observable<AiSettingsDto> {
    return this.http.put<AiSettingsDto>(this.SETTINGS_URL, data, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  deleteSettings(): Observable<void> {
    return this.http.delete<void>(this.SETTINGS_URL, { headers: this.getHeaders() })
      .pipe(catchError(this.handleError));
  }

  testConnection(): Observable<{ success: boolean; message: string; response?: string }> {
    return this.http.post<{ success: boolean; message: string; response?: string }>(
      `${this.SETTINGS_URL}/test`, {}, { headers: this.getHeaders() }
    ).pipe(catchError(this.handleError));
  }

  // --- SSE Streaming ---

  streamJournalReview(portfolioId?: string, question?: string): Observable<AiStreamChunk> {
    return this.streamRequest('journal-review', { portfolioId, question });
  }

  streamPortfolioReview(portfolioId: string, question?: string): Observable<AiStreamChunk> {
    return this.streamRequest('portfolio-review', { portfolioId, question });
  }

  streamTradePlanAdvisor(tradePlanId: string, question?: string): Observable<AiStreamChunk> {
    return this.streamRequest('trade-plan-advisor', { tradePlanId, question });
  }

  streamChat(message: string, history?: AiChatMessage[]): Observable<AiStreamChunk> {
    return this.streamRequest('chat', { message, history });
  }

  streamMonthlySummary(portfolioId: string, year: number, month: number): Observable<AiStreamChunk> {
    return this.streamRequest('monthly-summary', { portfolioId, year, month });
  }

  streamStockEvaluation(symbol: string, question?: string): Observable<AiStreamChunk> {
    return this.streamRequest('stock-evaluation', { symbol, question });
  }

  // --- Build Context (for copy-to-clipboard, no API key needed) ---

  buildContext(useCase: string, contextData: any): Observable<AiContextResult> {
    return this.http.post<AiContextResult>(`${this.AI_URL}/build-context`, {
      useCase,
      ...contextData
    }, { headers: this.getHeaders() }).pipe(catchError(this.handleError));
  }

  private streamRequest(endpoint: string, body: any): Observable<AiStreamChunk> {
    return new Observable(subscriber => {
      const token = this.authService.getToken();
      const abortController = new AbortController();

      fetch(`${this.AI_URL}/${endpoint}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`
        },
        body: JSON.stringify(body),
        signal: abortController.signal
      })
      .then(async response => {
        if (!response.ok) {
          try {
            const err = await response.json();
            subscriber.error(err);
          } catch {
            subscriber.error({ message: `HTTP ${response.status}` });
          }
          return;
        }

        const reader = response.body!.getReader();
        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          buffer += decoder.decode(value, { stream: true });
          const parts = buffer.split('\n\n');
          buffer = parts.pop()!;

          for (const part of parts) {
            const line = part.trim();
            if (!line.startsWith('data: ')) continue;
            const data = line.slice(6);
            if (data === '[DONE]') {
              subscriber.complete();
              return;
            }
            try {
              const chunk: AiStreamChunk = JSON.parse(data);
              subscriber.next(chunk);
            } catch { /* skip malformed */ }
          }
        }
        subscriber.complete();
      })
      .catch(err => {
        if (err.name !== 'AbortError') {
          subscriber.error(err);
        }
      });

      return () => abortController.abort();
    });
  }

  private handleError(error: any): Observable<never> {
    console.error('AI API error:', error);
    return throwError(() => error);
  }
}
