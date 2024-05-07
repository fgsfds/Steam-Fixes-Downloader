import { useEffect } from 'react';
import './App.css';

function App() {

    useEffect(() => {
    }, []);

    return (
        <div>
            <img src="logo.png" width="200"></img>
            <h1 id="tabelLabel">Steam Superheater</h1>

            <div className="horizontal-stack">

                <a href="https://github.com/fgsfds/Steam-Superheater">
                    <img className="with-margin" src="https://cdn-icons-png.flaticon.com/512/25/25231.png" height="100"></img>
                </a>

                <a href="https://discord.gg/mWvKyxR4et">
                    <img className="with-margin" src="https://static.vecteezy.com/system/resources/previews/023/741/066/original/discord-logo-icon-social-media-icon-free-png.png" height="100"></img>
                </a>
                
            </div>

        </div>
    );
}

export default App;