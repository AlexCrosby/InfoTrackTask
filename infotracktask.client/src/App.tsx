import { useState, useRef, type SubmitEvent, type ChangeEvent } from 'react';

const LOCATIONS = [
    'London',
    'Birmingham',
    'Leeds',
    'Manchester',
    'Sheffield',
    'Bradford',
    'Liverpool',
    'Bristol',
];
import './App.css';

// 1. Define the structural contract matching your C# SolicitorRecord class
interface SolicitorRecord {
    name: string;
    phoneNumber: string;
    address: string;
    website: string;
    email: string;
}

export default function App() {
    const [selectedLocations, setSelectedLocations] = useState<string[]>(['London']);
    const [results, setResults] = useState<SolicitorRecord[]>([]);
    const [loading, setLoading] = useState<boolean>(false);
    const abortControllerRef = useRef<AbortController | null>(null);

    const handleCheckboxChange = (e: ChangeEvent<HTMLInputElement>) => {
        const { value, checked } = e.target;
        setSelectedLocations(prev =>
            checked ? [...prev, value] : prev.filter(loc => loc !== value)
        );
    };

    const handleSearch = async (e: SubmitEvent<HTMLFormElement>) => {
        e.preventDefault();

        // Abort the previous stream request if it's currently running
        if (abortControllerRef.current) {
            abortControllerRef.current.abort();
        }

        const controller = new AbortController();
        abortControllerRef.current = controller;

        setLoading(true);
        setResults([]); // Reset state for a fresh run

        try {
            const locationQuery = selectedLocations.join(',');
            const response = await fetch(`/api/solicitors/stream?locations=${encodeURIComponent(locationQuery)}`, {
                signal: controller.signal
            });

            if (!response.ok) throw new Error("Network stream failure.");
            if (!response.body) throw new Error("ReadableStream is not supported by the endpoint response.");

            const reader = response.body.getReader();
            const decoder = new TextDecoder();
            let buffer = "";

            while (true) {
                const { value, done } = await reader.read();
                if (done) break;

                // Decode binary stream chunks into plain text fragments
                buffer += decoder.decode(value, { stream: true });

                let cleanText = buffer.trim();
                if (cleanText.startsWith('[')) cleanText = cleanText.substring(1);
                if (cleanText.endsWith(']')) cleanText = cleanText.substring(0, cleanText.length - 1);

                // Parse individual objects split out of the text buffer
                const jsonObjects: SolicitorRecord[] = cleanText
                    .split(/},\s*{/)
                    .map((str) => {
                        let validJson = str.trim();
                        if (!validJson.startsWith('{')) validJson = '{' + validJson;
                        if (!validJson.endsWith('}')) validJson = validJson + '}';
                        try {
                            return JSON.parse(validJson) as SolicitorRecord;
                        } catch {
                            return null; // Chunks split mid-object are ignored until the next pass completes them
                        }
                    })
                    .filter((item): item is SolicitorRecord => item !== null);

                if (jsonObjects.length > 0) {
                    setResults((prev) => {
                        // Deduplicate items on the client runtime array signature
                        const existingIds = new Set(prev.map(r => `${r.name}|${r.phoneNumber}`.toLowerCase()));
                        const filteredNew = jsonObjects.filter(r => !existingIds.has(`${r.name}|${r.phoneNumber}`.toLowerCase()));
                        return [...prev, ...filteredNew];
                    });
                }
            }
        } catch (err: any) {
            if (err.name === 'AbortError') {
                console.log("Stream search request aborted.");
            } else {
                console.error("Stream parsing error:", err);
            }
        } finally {
            if (abortControllerRef.current === controller) {
                setLoading(false);
                abortControllerRef.current = null;
            }
        }
    };

    return (
        <div className="app-container">
            <h2>Live Solicitor Stream Engine</h2>
            <p className="app-subtitle">Type locations to dynamically pull and merge rotated branch variations.</p>

            <form onSubmit={handleSearch} className="search-form">
                <div className="location-checkboxes">
                    {LOCATIONS.map(location => (
                        <label key={location} className="checkbox-label">
                            <input
                                type="checkbox"
                                value={location}
                                checked={selectedLocations.includes(location)}
                                onChange={handleCheckboxChange}
                                className="checkbox-input"
                            />
                            {location}
                        </label>
                    ))}
                </div>
                <button
                    type="submit"
                    className="search-button"
                    disabled={selectedLocations.length === 0}
                >
                    {loading ? 'Restart Search' : 'Gather Insights'}
                </button>
            </form>

            {loading && (
                <p className="status-loading">
                    The application is actively processing passes. New rows are appending below in real time...
                </p>
            )}

            <p>Total Unique Entries Found: <strong>{results.length}</strong></p>

            {results.length > 0 && (
                <table className="results-table">
                    <thead>
                        <tr>
                            <th>Name</th>
                            <th>Phone</th>
                            <th>Address</th>
                            <th>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        {results.map((solicitor, index) => (
                            <tr key={index}>
                                <td className="cell-name">{solicitor.name}</td>
                                <td>{solicitor.phoneNumber || 'N/A'}</td>
                                <td className="cell-address">{solicitor.address}</td>
                                <td>
                                    <div className="cell-actions">
                                        {solicitor.website && (
                                            <a href={solicitor.website} target="_blank" rel="noreferrer" className="link-website">Website</a>
                                        )}
                                        {solicitor.email && (
                                            <a href={solicitor.email} target="_blank" rel="noreferrer" className="link-contact">Contact</a>
                                        )}
                                    </div>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
        </div>
    );
}