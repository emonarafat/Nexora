import { getApiBaseUrl } from "./searchApi";

type AnalyticsEventType = "search_result_click" | "search_add_to_cart";

interface AnalyticsEventPayload {
  eventType: AnalyticsEventType;
  productId: string;
  position: number;
  page: number;
  queryLength: number;
  queryTokenCount: number;
  sentAtUtc: string;
}

function getQueryMeta(query: string): { queryLength: number; queryTokenCount: number } {
  const trimmed = query.trim();
  return {
    queryLength: trimmed.length,
    queryTokenCount: trimmed.length === 0 ? 0 : trimmed.split(/\s+/).length,
  };
}

export async function emitSearchAnalyticsEvent(
  eventType: AnalyticsEventType,
  query: string,
  productId: string,
  position: number,
  page: number,
): Promise<void> {
  const payload: AnalyticsEventPayload = {
    eventType,
    productId,
    position,
    page,
    ...getQueryMeta(query),
    sentAtUtc: new Date().toISOString(),
  };

  const endpoint = `${getApiBaseUrl()}/api/v1/analytics/events`;

  try {
    const body = JSON.stringify(payload);
    if (typeof navigator !== "undefined" && typeof navigator.sendBeacon === "function") {
      const blob = new Blob([body], { type: "application/json" });
      navigator.sendBeacon(endpoint, blob);
      return;
    }

    await fetch(endpoint, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body,
      keepalive: true,
    });
  } catch {
    // Best-effort analytics: do not block search UX when telemetry endpoint is unavailable.
  }
}
