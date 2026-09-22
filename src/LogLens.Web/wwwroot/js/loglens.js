// Download und Zwischenablage ohne Netzwerkzugriff: alles bleibt im Browser.

export function downloadText(fileName, content, contentType) {
  const blob = new Blob([content], { type: contentType });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.style.display = "none";
  document.body.appendChild(link);
  link.click();
  link.remove();
  // Erst nach dem Klick freigeben, sonst bricht Safari den Download ab.
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

export async function copyText(content) {
  try {
    await navigator.clipboard.writeText(content);
    return true;
  } catch {
    return false;
  }
}
